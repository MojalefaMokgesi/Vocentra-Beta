using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace Vocentra.Services
{
    public static class PayFastSecurity
    {
        public static string BuildSignature(IDictionary<string, string> data, string? passPhrase)
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