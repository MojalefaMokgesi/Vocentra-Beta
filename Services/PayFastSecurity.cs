using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Vocentra.Services
{
    public static class PayFastSecurity
    {
        /// <summary>
        /// PayFast signature:
        /// - Exclude "signature"
        /// - Exclude empty/null values
        /// - Sort by key (ascending, ordinal)
        /// - Build key=value&key2=value2... with application/x-www-form-urlencoded encoding on VALUES
        /// - If passphrase is set, append &passphrase=...
        /// - MD5 over ASCII
        /// </summary>
        public static string BuildSignature(IDictionary<string, string> data, string? passPhrase)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // application/x-www-form-urlencoded encoding (matches HTML form posts / PHP urlencode)
            static string Encode(string value)
            {
                // WebUtility.UrlEncode produces + for spaces (important)
                // Ensure null-safe
                return WebUtility.UrlEncode(value ?? string.Empty) ?? string.Empty;
            }

            var filtered = data
                .Where(kv => !kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);

            var param = string.Join("&", filtered.Select(kv => $"{kv.Key}={Encode(kv.Value)}"));

            if (!string.IsNullOrWhiteSpace(passPhrase))
            {
                param += $"&passphrase={Encode(passPhrase)}";
            }

            using var md5 = MD5.Create();

            // PayFast examples align best with ASCII
            var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(param));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
