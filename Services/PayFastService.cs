using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Globalization;
using Vocentra.Models;

namespace Vocentra.Services
{
    public class PayFastService
    {
        private readonly PayFastOptions _opts;
        private readonly IHttpClientFactory _httpClientFactory;

        public PayFastService(IOptions<PayFastOptions> opts, IHttpClientFactory httpClientFactory)
        {
            _opts = opts.Value;
            _httpClientFactory = httpClientFactory;
        }

        public string ProcessUrl => _opts.UseSandbox
            ? "https://sandbox.payfast.co.za/eng/process"
            : "https://www.payfast.co.za/eng/process";

        public string ValidateUrl => _opts.UseSandbox
            ? "https://sandbox.payfast.co.za/eng/query/validate"
            : "https://www.payfast.co.za/eng/query/validate";

        // Return BOTH: ordered list (for signature) + dictionary (easy form rendering)
        public (List<KeyValuePair<string, string>> Ordered, Dictionary<string, string> Fields) BuildRequestFields(
            string merchantReference,
            decimal amountZar,
            string itemName)
        {
            // Build in the exact order you will post.
            var ordered = new List<KeyValuePair<string, string>>
            {
                // Merchant details
                new("merchant_id",  _opts.MerchantId),
                new("merchant_key", _opts.MerchantKey),
                new("return_url",   _opts.ReturnUrl),
                new("cancel_url",   _opts.CancelUrl),
                new("notify_url",   _opts.NotifyUrl),

                // Transaction details
                new("m_payment_id", merchantReference),
                new("amount",       amountZar.ToString("0.00", CultureInfo.InvariantCulture)),
                new("item_name",    itemName),
            };

            // Calculate signature from ORDERED data (no sorting)
            var signature = PayFastSecurity.BuildSignature(ordered, _opts.PassPhrase);
            ordered.Add(new KeyValuePair<string, string>("signature", signature));

            // Dictionary for rendering inputs (kept consistent)
            var dict = ordered.ToDictionary(k => k.Key, v => v.Value);

            return (ordered, dict);
        }

        // ITN signature verification: you MUST verify using the same order PayFast sent.
        // Best practice: in your controller, read Request.Form IN ORDER and pass that ordered list here.
        public bool VerifySignature(List<KeyValuePair<string, string>> itnOrdered)
        {
            var received = itnOrdered.FirstOrDefault(kv => kv.Key.Equals("signature", System.StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrWhiteSpace(received)) return false;

            var calc = PayFastSecurity.BuildSignature(itnOrdered, _opts.PassPhrase);
            return string.Equals(received, calc, System.StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> ValidateItnAsync(Dictionary<string, string> itnDict, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient();
            using var resp = await client.PostAsync(ValidateUrl, new FormUrlEncodedContent(itnDict), ct);
            var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
            return resp.IsSuccessStatusCode && body.Equals("VALID", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
