using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Vocentra.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings?.Value ?? new EmailSettings();
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Basic SMTP send - logs and does best-effort. Exceptions are logged but not rethrown to avoid crashing flows.
            try
            {
                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.UseSsl
                };

                if (!string.IsNullOrWhiteSpace(_settings.UserName))
                {
                    client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
                }

                var from = _settings.From ?? _settings.UserName ?? "noreply@localhost";
                var msg = new MailMessage(from, email, subject, htmlMessage)
                {
                    IsBodyHtml = true
                };

                client.Send(msg);
                _logger?.LogInformation("Sent email to {Email} via SMTP host {Host}", email, _settings.Host);
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Failed to send email to {Email}", email);
            }

            return Task.CompletedTask;
        }
    }
}
