using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Vocentra.Services
{
    public static class PayFastSecurity
    {
        // Encode to match PayFast:
        // - Spaces become '+'
        // - Percent-encoding must be UPPERCASE (e.g. %3A not %3a)
        private static string PayFastEncode(string value)
        {
            if (value == null) return string.Empty;

            // EscapeDataString uses %20 for spaces; PayFast expects '+'
            var encoded = Uri.EscapeDataString(value);

            // Uppercase any %xx sequences
            // (Uri.EscapeDataString is usually uppercase already, but we force it)
            var sb = new StringBuilder(encoded.Length);
            for (int i = 0; i < encoded.Length; i++)
            {
                char c = encoded[i];
                if (c == '%' && i + 2 < encoded.Length)
                {
                    sb.Append('%');
                    sb.Append(char.ToUpperInvariant(encoded[i + 1]));
                    sb.Append(char.ToUpperInvariant(encoded[i + 2]));
                    i += 2;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Replace("%20", "+");
        }

        /// <summary>
        /// IMPORTANT:
        /// For /eng/process signature, PayFast expects parameters in the "attribute description order",
        /// NOT alphabetical order. So we DO NOT sort.
        /// </summary>
        public static string BuildSignature(IEnumerable<KeyValuePair<string, string>> data, string? passPhrase)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var parts = new List<string>();

            foreach (var kv in data)
            {
                if (kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(kv.Value))
                    continue;

                parts.Add($"{kv.Key}={PayFastEncode(kv.Value)}");
            }

            var param = string.Join("&", parts);

            if (!string.IsNullOrWhiteSpace(passPhrase))
            {
                param += $"&passphrase={PayFastEncode(passPhrase)}";
            }

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(param));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // Backwards compatible overload
        public static string BuildSignature(IDictionary<string, string> data, string? passPhrase)
            => BuildSignature(data.AsEnumerable(), passPhrase);
    }
}
