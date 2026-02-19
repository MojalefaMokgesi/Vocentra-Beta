using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<NotificationService> _log;

        public NotificationService(AppDbContext db, ILogger<NotificationService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task CreateNotificationAsync(string userId, string title, string body, string? link = null)
        {
            try
            {
                var n = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Body = body,
                    LinkUrl = link,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Notifications.Add(n);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to create notification");
            }
        }
    }
}
