using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Vocentra.Services
{
    // Simple no-op email sender to avoid DI failures when email is not configured.
    public class NullEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Intentionally do nothing. In production, replace with a real email sender.
            return Task.CompletedTask;
        }
    }
}
