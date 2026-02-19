using System;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Vocentra.Services
{
    public class AdminGateService : IAdminGateService
    {
        private const string CookieName = "vocentra_admin_gate";
        private readonly IDataProtector _protector;
        private readonly IHostEnvironment _env;
        private readonly AdminAccessOptions _opts;

        public AdminGateService(IDataProtectionProvider dp, IHostEnvironment env, IOptions<AdminAccessOptions> opts)
        {
            _protector = dp.CreateProtector("AdminGateProtection.v1");
            _env = env;
            _opts = opts.Value ?? new AdminAccessOptions();
        }

        public bool IsVerified(HttpContext context)
        {
            if (context == null) return false;
            if (!context.Request.Cookies.TryGetValue(CookieName, out var value)) return false;

            try
            {
                var payload = _protector.Unprotect(value);
                // payload format: userId|expiryTicks
                var parts = payload.Split('|');
                if (parts.Length != 2) return false;
                var userId = parts[0];
                if (string.IsNullOrEmpty(userId)) return false;
                if (!long.TryParse(parts[1], out var ticks)) return false;
                var expiry = new DateTime(ticks, DateTimeKind.Utc);
                if (DateTime.UtcNow > expiry) return false;

                var currentUser = context.User?.Identity?.IsAuthenticated == true ? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;
                if (string.IsNullOrEmpty(currentUser)) return false;

                return string.Equals(currentUser, userId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public void MarkVerified(HttpContext context)
        {
            var userId = context.User?.Identity?.IsAuthenticated == true ? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;
            if (string.IsNullOrEmpty(userId)) return;

            var expiry = DateTime.UtcNow.AddHours(8);
            var payload = userId + "|" + expiry.Ticks.ToString();
            var protectedValue = _protector.Protect(payload);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Expires = expiry,
                IsEssential = true,
                Path = "/"
            };

            context.Response.Cookies.Append(CookieName, protectedValue, cookieOptions);
        }

        public void Clear(HttpContext context)
        {
            context.Response.Cookies.Delete(CookieName);
        }

        public bool VerifyCode(string input, string configured)
        {
            if (input == null || configured == null) return false;
            var a = Encoding.UTF8.GetBytes(input);
            var b = Encoding.UTF8.GetBytes(configured);

            if (a.Length != b.Length)
            {
                // Constant-time compare requires equal-length inputs; compare with padded values
                var max = Math.Max(a.Length, b.Length);
                var aa = new byte[max];
                var bb = new byte[max];
                Buffer.BlockCopy(a, 0, aa, 0, a.Length);
                Buffer.BlockCopy(b, 0, bb, 0, b.Length);
                return CryptographicOperations.FixedTimeEquals(aa, bb);
            }

            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
