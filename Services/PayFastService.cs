using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        public IDictionary<string, string> BuildRequestFields(
            string merchantReference,
            decimal amountZar,
            string itemName)
        {
            // Keep values EXACTLY what you will POST.
            // Amount must be 0.00 with invariant culture.
            var fields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["merchant_id"] = _opts.MerchantId?.Trim() ?? string.Empty,
                ["merchant_key"] = _opts.MerchantKey?.Trim() ?? string.Empty,

                ["return_url"] = _opts.ReturnUrl?.Trim() ?? string.Empty,
                ["cancel_url"] = _opts.CancelUrl?.Trim() ?? string.Empty,
                ["notify_url"] = _opts.NotifyUrl?.Trim() ?? string.Empty,

                ["m_payment_id"] = merchantReference?.Trim() ?? string.Empty,
                ["amount"] = amountZar.ToString("0.00", CultureInfo.InvariantCulture),
                ["item_name"] = itemName ?? string.Empty
            };

            // Build signature LAST, after all fields are final
            fields["signature"] = PayFastSecurity.BuildSignature(fields, _opts.PassPhrase);

            return fields;
        }

        public bool VerifySignature(IDictionary<string, string> itnData)
        {
            if (itnData == null) return false;

            itnData.TryGetValue("signature", out var received);
            received = received?.Trim();

            if (string.IsNullOrWhiteSpace(received)) return false;

            var calc = PayFastSecurity.BuildSignature(itnData, _opts.PassPhrase);

            return string.Equals(received, calc, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> ValidateItnAsync(IDictionary<string, string> itnData, CancellationToken ct = default)
        {
            // PayFast validation expects you to POST back what you received (including signature).
            var client = _httpClientFactory.CreateClient();

            using var resp = await client.PostAsync(
                ValidateUrl,
                new FormUrlEncodedContent(itnData),
                ct);

            var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();

            return resp.IsSuccessStatusCode && body.Equals("VALID", StringComparison.OrdinalIgnoreCase);
        }
    }
}
