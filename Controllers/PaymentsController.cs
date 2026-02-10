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
        // IMPORTANT: Require auth, so a payment is linked to a real user.
        [Authorize]
        [HttpPost("/jobs/{id:int}/pay")]
        public async Task<IActionResult> PayJob(int id)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            // Don’t start a new payment if already active/paid
            if (job.IsPaid && string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Job is already active/paid.");

            // LOCK PRICING AT PAY-TIME (prevents midnight mismatch)
            // If you prefer local SA date, use DateTime.Now.Date consistently everywhere.
            var startDate = DateTime.UtcNow.Date;
            if (!job.ApplicationDeadline.HasValue)
                return BadRequest("Application deadline is required.");

            var endDate = job.ApplicationDeadline.Value.Date;


            var days = Pricing.DaysInclusive(startDate, endDate);
            if (days <= 0) return BadRequest("Deadline must be today or later.");

            var amount = days * Pricing.RatePerDayZar;

            // Create unique merchant reference (human readable)
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

            var fields = new Dictionary<string, string>
            {
                ["merchant_id"] = _opts.MerchantId,
                ["merchant_key"] = _opts.MerchantKey,
                ["return_url"] = _opts.ReturnUrl,
                ["cancel_url"] = _opts.CancelUrl,
                ["notify_url"] = _opts.NotifyUrl,
                ["m_payment_id"] = merchantRef,
                ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["item_name"] = $"Job {job.Title}"
            };

            // Sign request
            fields["signature"] = BuildSignature(fields, _opts.PassPhrase);

            var html = BuildAutoPostHtml(payFastUrl, fields);
            return Content(html, "text/html");
        }

        [HttpGet("return")]
        public IActionResult Return()
        {
            // NEVER activate here.
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

            // Copy posted fields
            var itn = Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString());

            // 1) Signature verification
            if (!VerifySignature(itn, _opts.PassPhrase))
                return Ok();

            // 2) Find transaction
            itn.TryGetValue("m_payment_id", out var mPaymentId);
            if (string.IsNullOrWhiteSpace(mPaymentId)) return Ok();

            var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.MerchantReference == mPaymentId);
            if (tx == null) return Ok();

            // Idempotent: PayFast may resend ITN
            if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return Ok();

            // 3) payment_status must be COMPLETE
            itn.TryGetValue("payment_status", out var paymentStatus);
            if (!string.Equals(paymentStatus, "COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                // Don’t activate. Just store the status and wait for a later ITN update.
                tx.Status = paymentStatus; // e.g. Pending / Failed / etc
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

                // Optional: track paid until
                job.PaidUntil = tx.EndDate;
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // -------------------------
        // Helpers
        // -------------------------
        private static string BuildAutoPostHtml(string actionUrl, IDictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body onload=\"document.forms[0].submit()\">");
            sb.AppendLine($"<form method=\"post\" action=\"{WebUtility.HtmlEncode(actionUrl)}\">");

            foreach (var kv in fields)
            {
                sb.AppendLine($"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(kv.Key)}\" value=\"{WebUtility.HtmlEncode(kv.Value)}\" />");
            }

            sb.AppendLine("</form></body></html>");
            return sb.ToString();
        }

        private static bool VerifySignature(IDictionary<string, string> itnData, string? passPhrase)
        {
            if (!itnData.TryGetValue("signature", out var received) || string.IsNullOrWhiteSpace(received))
                return false;

            var calc = BuildSignature(itnData, passPhrase);
            return string.Equals(received, calc, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSignature(IDictionary<string, string> data, string? passPhrase)
        {
            // Remove signature and empty values
            var filtered = data
                .Where(kv => !kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);

            static string Encode(string s) => Uri.EscapeDataString(s).Replace("%20", "+");

            var param = string.Join("&", filtered.Select(kv => $"{kv.Key}={Encode(kv.Value)}"));

            if (!string.IsNullOrWhiteSpace(passPhrase))
                param += $"&passphrase={Encode(passPhrase)}";

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(param));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
