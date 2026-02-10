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

            // Build the exact fields to POST (trim + remove empties)
            var fields = BuildPayFastFields(job, merchantRef, amount);

            // Generate signature from the same fields (canonical string)
            var signatureBase = BuildSignatureBaseString(fields, _opts.PassPhrase);
            var signature = Md5Hex(signatureBase);
            fields["signature"] = signature;

            // Optional: quick diagnostic (enable temporarily)
            // Console.WriteLine("PF signature base: " + signatureBase);
            // Console.WriteLine("PF signature md5:  " + signature);

            var html = BuildAutoPostHtml(payFastUrl, fields);
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

            var itn = Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString());

            // 1) Signature verification (PayFast style)
            if (!VerifySignature(itn, _opts.PassPhrase))
                return Ok();

            // 2) Find transaction
            itn.TryGetValue("m_payment_id", out var mPaymentId);
            if (string.IsNullOrWhiteSpace(mPaymentId)) return Ok();

            var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.MerchantReference == mPaymentId);
            if (tx == null) return Ok();

            // Idempotent
            if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return Ok();

            // 3) payment_status must be COMPLETE
            itn.TryGetValue("payment_status", out var paymentStatus);
            if (!string.Equals(paymentStatus, "COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                tx.Status = paymentStatus ?? "Unknown";
                await _db.SaveChangesAsync();
                return Ok();
            }

            // 4) Amount match against LOCKED transaction amount
            itn.TryGetValue("amount_gross", out var amountGrossStr);
            if (!decimal.TryParse(amountGrossStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountGross))
                return Ok();

            if (amountGross != tx.AmountZar)
                return Ok();

            // 5) Validate with PayFast (post-back)
            var validateUrl = _opts.UseSandbox
                ? "https://sandbox.payfast.co.za/eng/query/validate"
                : "https://www.payfast.co.za/eng/query/validate";

            var http = _httpClientFactory.CreateClient();
            using var resp = await http.PostAsync(validateUrl, new FormUrlEncodedContent(itn));
            var body = (await resp.Content.ReadAsStringAsync()).Trim();

            if (!resp.IsSuccessStatusCode || !body.Equals("VALID", StringComparison.OrdinalIgnoreCase))
                return Ok();

            // 6) Mark paid + activate job
            itn.TryGetValue("pf_payment_id", out var pfPaymentId);

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
        // Helpers
        // -------------------------

        private Dictionary<string, string> BuildPayFastFields(Job job, string merchantRef, decimal amount)
        {
            // Sorted by key (ordinal) makes signature reproducible
            var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["merchant_id"] = (_opts.MerchantId ?? "").Trim(),
                ["merchant_key"] = (_opts.MerchantKey ?? "").Trim(),
                ["return_url"] = (_opts.ReturnUrl ?? "").Trim(),
                ["cancel_url"] = (_opts.CancelUrl ?? "").Trim(),
                ["notify_url"] = (_opts.NotifyUrl ?? "").Trim(),
                ["m_payment_id"] = merchantRef.Trim(),
                ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["item_name"] = $"Job {job.Title}".Trim()
            };

            // Remove empty values (PayFast expects empties excluded from signature string)
            var cleaned = dict
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Trim(), StringComparer.Ordinal);

            return cleaned;
        }

        private static string BuildAutoPostHtml(string actionUrl, IDictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body onload=\"document.forms[0].submit()\">");
            sb.AppendLine($"<form method=\"post\" action=\"{WebUtility.HtmlEncode(actionUrl)}\">");

            // Post fields in stable key order
            foreach (var kv in fields.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(
                    $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(kv.Key)}\" value=\"{WebUtility.HtmlEncode(kv.Value)}\" />");
            }

            sb.AppendLine("</form></body></html>");
            return sb.ToString();
        }

        private static bool VerifySignature(IDictionary<string, string> data, string? passPhrase)
        {
            if (!data.TryGetValue("signature", out var received) || string.IsNullOrWhiteSpace(received))
                return false;

            var signatureBase = BuildSignatureBaseString(data, passPhrase);
            var calc = Md5Hex(signatureBase);

            return string.Equals(received, calc, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// PayFast expects signature string built like PHP urlencode (application/x-www-form-urlencoded):
        /// - exclude signature
        /// - exclude empty values
        /// - sort by key (ordinal)
        /// - key=value&key=value...
        /// - append &passphrase=... only if passphrase is non-empty
        /// </summary>
        private static string BuildSignatureBaseString(IDictionary<string, string> data, string? passPhrase)
        {
            var filtered = data
                .Where(kv => !kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                .Select(kv => new KeyValuePair<string, string>(kv.Key.Trim(), (kv.Value ?? "").Trim()))
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);

            var param = string.Join("&", filtered.Select(kv => $"{kv.Key}={FormUrlEncodePhpStyle(kv.Value)}"));

            if (!string.IsNullOrWhiteSpace(passPhrase))
                param += $"&passphrase={FormUrlEncodePhpStyle(passPhrase.Trim())}";

            return param;
        }

        private static string Md5Hex(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// PHP urlencode equivalent (RFC1738):
        /// - space => +
        /// - percent-encoding uses uppercase hex (matches typical PHP urlencode output)
        /// - ~ remains unescaped
        /// </summary>
        private static string FormUrlEncodePhpStyle(string value)
        {
            // WebUtility.UrlEncode uses application/x-www-form-urlencoded (space => +)
            // but also encodes using lowercase/uppercase depending on framework; normalize percent hex to uppercase.
            var encoded = WebUtility.UrlEncode(value) ?? "";

            // Keep ~ as ~ (some encoders encode it; PayFast/PHP typically leaves it)
            encoded = encoded.Replace("%7e", "~").Replace("%7E", "~");

            // Normalize percent hex to uppercase for consistency
            // (PayFast signature is based on exact string)
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
