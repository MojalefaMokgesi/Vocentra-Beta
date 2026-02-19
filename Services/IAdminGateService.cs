using Microsoft.AspNetCore.Http;

namespace Vocentra.Services
{
    public interface IAdminGateService
    {
        bool IsVerified(HttpContext context);
        void MarkVerified(HttpContext context);
        void Clear(HttpContext context);
        bool VerifyCode(string input, string configured);
    }
}
