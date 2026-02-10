using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;

namespace Vocentra.Controllers
{
    [Route("payments/payfast")]
    public class PaymentsController : Controller
    {
        private readonly PayFastOptions _opts;
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public PaymentsController(IOptions<PayFastOptions> opts, AppDbContext db, IHttpClientFactory httpClientFactory)
        {
            _opts = opts.Value;
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        // -------------------------
        // PAY (creates pending tx)
        // -------------------------
        [Authorize]
        [HttpPost("/jobs/{id:int}/pay")]
        public async Task<IActionResult> PayJob(int id)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            if (job.IsPaid && string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Job is already active/paid.");

            if (!job.ApplicationDeadline.HasValue)
                return BadRequest("Application deadline is required.");

            var startDate = DateTime.UtcNow.Date;
            var endDate = job.ApplicationDeadline.Value.Date;

            var days = Pricing.DaysInclusive(startDate, endDate);
            if (days <= 0) return BadRequest("Deadline must be today or later.");

            var amount = days * Pricing.RatePerDayZar;
            var merchantRef = $"JOB-{job.Id}-{Guid.NewGuid():N}";

            // Create transaction record BEFORE redirect
            var tx = new PaymentTransaction
            {
                JobId = job.Id,
                UserId = userId,
                AmountZar = amount,
                DaysPaid = days,
                StartDate = startDate,
                EndDate = endDate,
                MerchantReference = merchantRef,
                Provider = "PayFast",
                Status = "Pending"
            };
            _db.PaymentTransactions.Add(tx);

            // Force draft state until ITN confirms
            job.IsPaid = false;
            job.Status = "Draft";
            job.PaymentStatus = "Pending";

            await _db.SaveChangesAsync();

            var payFastUrl = _opts.UseSandbox
                ? "https://sandbox.payfast.co.za/eng/process"
                : "https://www.payfast.co.za/eng/process";

            // Build ordered fields EXACTLY as you will post them
            var orderedFields = BuildPayFastFieldsOrdered(job, merchantRef, amount);

            // Signature must be computed from the same ordered fields
            var sigBase = BuildSignatureBaseStringOrdered(orderedFields, _opts.PassPhrase);
            var signature = Md5Hex(sigBase);

            orderedFields.Add(new KeyValuePair<string, string>("signature", signature));

            var html = BuildAutoPostHtmlOrdered(payFastUrl, orderedFields);
            return Content(html, "text/html");
        }

        [HttpGet("return")]
        public IActionResult Return()
        {
            return Content("Payment received by PayFast. Waiting for confirmation (ITN). Check your dashboard shortly.");
        }

        [HttpGet("cancel")]
        public IActionResult Cancel()
        {
            return Content("Payment cancelled.");
        }

        // -------------------------
        // ITN NOTIFY (activates job)
        // -------------------------
        [AllowAnonymous]
        [HttpPost("notify")]
        public async Task<IActionResult> Notify()
        {
            if (!Request.HasFormContentType) return Ok();

            // IMPORTANT: preserve the posted order
            var itnOrdered = Request.Form
                .Select(k => new KeyValuePair<string, string>(k.Key, k.Value.ToString()))
                .ToList();

            // Also keep a dictionary for convenience
            var itnDict = itnOrdered
                .GroupBy(x => x.Key, StringComparer.Ordinal) // avoid duplicate key crash
                .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);

            // 1) Signature verification (PayFast style: iterate until signature; do NOT sort)
            if (!VerifySignatureItn(itnOrdered, _opts.PassPhrase))
                return Ok();

            // 2) Find transaction
            itnDict.TryGetValue("m_payment_id", out var mPaymentId);
            if (string.IsNullOrWhiteSpace(mPaymentId)) return Ok();

            var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.MerchantReference == mPaymentId);
            if (tx == null) return Ok();

            // Idempotent
            if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return Ok();

            // 3) payment_status must be COMPLETE
            itnDict.TryGetValue("payment_status", out var paymentStatus);
            if (!string.Equals(paymentStatus, "COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                tx.Status = paymentStatus ?? "Unknown";
                await _db.SaveChangesAsync();
                return Ok();
            }

            // 4) Amount match against LOCKED transaction amount
            itnDict.TryGetValue("amount_gross", out var amountGrossStr);
            if (!decimal.TryParse(amountGrossStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountGross))
                return Ok();

            if (amountGross != tx.AmountZar)
                return Ok();

            // 5) Validate with PayFast (post-back)
            var validateUrl = _opts.UseSandbox
                ? "https://sandbox.payfast.co.za/eng/query/validate"
                : "https://www.payfast.co.za/eng/query/validate";

            var http = _httpClientFactory.CreateClient();
            using var resp = await http.PostAsync(validateUrl, new FormUrlEncodedContent(itnDict));
            var body = (await resp.Content.ReadAsStringAsync()).Trim();

            if (!resp.IsSuccessStatusCode || !body.Equals("VALID", StringComparison.OrdinalIgnoreCase))
                return Ok();

            // 6) Mark paid + activate job
            itnDict.TryGetValue("pf_payment_id", out var pfPaymentId);

            tx.Status = "Paid";
            tx.ProviderPaymentId = pfPaymentId;
            tx.PaidAtUtc = DateTime.UtcNow;

            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == tx.JobId);
            if (job != null)
            {
                job.IsPaid = true;
                job.Status = "Active";
                job.PaidAt = DateTime.UtcNow;
                job.PaymentStatus = "Paid";
                job.PaidUntil = tx.EndDate;
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // -------------------------
        // Helpers (ORDERED)
        // -------------------------

        // Build fields in the PayFast "form spec" order (do not sort)
        private List<KeyValuePair<string, string>> BuildPayFastFieldsOrdered(Job job, string merchantRef, decimal amount)
        {
            string Clean(string? s) => (s ?? "").Trim();

            var fields = new List<KeyValuePair<string, string>>
            {
                // Merchant details
                new("merchant_id",  Clean(_opts.MerchantId)),
                new("merchant_key", Clean(_opts.MerchantKey)),
                new("return_url",   Clean(_opts.ReturnUrl)),
                new("cancel_url",   Clean(_opts.CancelUrl)),
                new("notify_url",   Clean(_opts.NotifyUrl)),

                // Transaction details
                new("m_payment_id", Clean(merchantRef)),
                new("amount",       amount.ToString("0.00", CultureInfo.InvariantCulture)),
                new("item_name",    Clean($"Job {job.Title}"))
            };

            // Remove empty values (PayFast excludes blanks from signature string)
            return fields.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToList();
        }

        private static string BuildAutoPostHtmlOrdered(string actionUrl, List<KeyValuePair<string, string>> fields)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body onload=\"document.forms[0].submit()\">");
            sb.AppendLine($"<form method=\"post\" action=\"{WebUtility.HtmlEncode(actionUrl)}\">");

            // IMPORTANT: output in the same order used for signature
            foreach (var kv in fields)
            {
                sb.AppendLine(
                    $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(kv.Key)}\" value=\"{WebUtility.HtmlEncode(kv.Value)}\" />");
            }

            sb.AppendLine("</form></body></html>");
            return sb.ToString();
        }

        // Build signature base string from ORDERED fields (no sorting)
        private static string BuildSignatureBaseStringOrdered(List<KeyValuePair<string, string>> fields, string? passPhrase)
        {
            var parts = new List<string>();

            foreach (var kv in fields)
            {
                if (kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = (kv.Key ?? "").Trim();
                var val = (kv.Value ?? "").Trim();

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val))
                    continue;

                parts.Add($"{key}={FormUrlEncodePhpStyle(val)}");
            }

            var param = string.Join("&", parts);

            if (!string.IsNullOrWhiteSpace(passPhrase))
                param += $"&passphrase={FormUrlEncodePhpStyle(passPhrase.Trim())}";

            return param;
        }

        // ITN signature: PayFast PHP sample iterates until "signature" key, and does NOT sort.
        private static bool VerifySignatureItn(List<KeyValuePair<string, string>> itnOrdered, string? passPhrase)
        {
            var received = itnOrdered.FirstOrDefault(x => x.Key.Equals("signature", StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrWhiteSpace(received)) return false;

            // Rebuild param string in received order, stopping when we reach signature
            var parts = new List<string>();

            foreach (var kv in itnOrdered)
            {
                if (kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                    break;

                var key = (kv.Key ?? "").Trim();
                var val = (kv.Value ?? "").Trim();

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val))
                    continue;

                parts.Add($"{key}={FormUrlEncodePhpStyle(val)}");
            }

            var param = string.Join("&", parts);

            if (!string.IsNullOrWhiteSpace(passPhrase))
                param += $"&passphrase={FormUrlEncodePhpStyle(passPhrase.Trim())}";

            var calc = Md5Hex(param);
            return string.Equals(received.Trim(), calc, StringComparison.OrdinalIgnoreCase);
        }

        private static string Md5Hex(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // PHP urlencode equivalent (RFC1738-ish): space => +, normalize %xx to uppercase, keep ~
        private static string FormUrlEncodePhpStyle(string value)
        {
            var encoded = WebUtility.UrlEncode(value) ?? "";
            encoded = encoded.Replace("%7e", "~").Replace("%7E", "~");

            var sb = new StringBuilder(encoded.Length);
            for (int i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] == '%' && i + 2 < encoded.Length)
                {
                    sb.Append('%');
                    sb.Append(char.ToUpperInvariant(encoded[i + 1]));
                    sb.Append(char.ToUpperInvariant(encoded[i + 2]));
                    i += 2;
                }
                else
                {
                    sb.Append(encoded[i]);
                }
            }
            return sb.ToString();
        }
    }
}
