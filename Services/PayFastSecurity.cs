using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Vocentra.Services
{
    public static class PayFastSecurity
    {
        // IMPORTANT:
        // PayFast "Custom Integration" checkout signature expects parameters in the SAME ORDER
        // you submit them (spec order), NOT alphabetical ordering.
        public static string BuildSignature(IEnumerable<KeyValuePair<string, string>> orderedData, string? passPhrase)
        {
            static string Encode(string s)
                => Uri.EscapeDataString(s).Replace("%20", "+"); // spaces must be '+'

            var filtered = orderedData
                .Where(kv => !kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value));

            var paramString = string.Join("&", filtered.Select(kv => $"{kv.Key}={Encode(kv.Value.Trim())}"));

            // Only append passphrase if you actually have one set on PayFast
            if (!string.IsNullOrWhiteSpace(passPhrase))
                paramString += $"&passphrase={Encode(passPhrase.Trim())}";

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(paramString));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
