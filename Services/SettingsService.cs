using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Services
{
    public class SettingsService
    {
        private readonly AppDbContext _db;
        private readonly IServiceProvider _services;

        public SettingsService(AppDbContext db, IServiceProvider services)
        {
            _db = db;
            _services = services;
        }

        public async Task<string?> GetAsync(string key)
        {
            var s = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
            return s?.Value;
        }

        public async Task SetAsync(string key, string value, string? description = null)
        {
            var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
            if (s == null)
            {
                s = new Setting { Key = key, Value = value, Description = description };
                _db.Settings.Add(s);
            }
            else
            {
                s.Value = value;
                if (description != null) s.Description = description;
                _db.Settings.Update(s);
            }
            await _db.SaveChangesAsync();
        }

        public async Task<string?> GetUserSettingAsync(string userId, string key)
        {
            var s = await _db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (s == null) return null;

            return key?.ToLowerInvariant() switch
            {
                "emailnotifications" => s.EmailNotifications.ToString(),
                "jobalerts" => s.JobAlerts.ToString(),
                "alertfrequency" => s.AlertFrequency,
                "alertcategories" => s.AlertCategories,
                "alertlocations" => s.AlertLocations,
                "preferredcontactmethod" => s.PreferredContactMethod,
                "profilevisibility" => s.ProfileVisibility,
                "showemail" => s.ShowEmail.ToString(),
                "showphone" => s.ShowPhone.ToString(),
                "allowcvdownload" => s.AllowCvDownload.ToString(),
                "appearinsearch" => s.AppearInSearch.ToString(),
                _ => null,
            };
        }

        public async Task SetUserSettingAsync(string userId, string key, string value)
        {
            var s = await _db.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId);
            if (s == null)
            {
                s = new Models.UserSetting { UserId = userId };
                _db.UserSettings.Add(s);
            }

            switch (key?.ToLowerInvariant())
            {
                case "emailnotifications":
                    if (bool.TryParse(value, out var b1)) s.EmailNotifications = b1;
                    break;
                case "jobalerts":
                    if (bool.TryParse(value, out var b2)) s.JobAlerts = b2;
                    break;
                case "alertfrequency":
                    s.AlertFrequency = value;
                    break;
                case "alertcategories":
                    s.AlertCategories = value;
                    break;
                case "alertlocations":
                    s.AlertLocations = value;
                    break;
                case "preferredcontactmethod":
                    s.PreferredContactMethod = value;
                    break;
                case "profilevisibility":
                    s.ProfileVisibility = value;
                    break;
                case "showemail":
                    if (bool.TryParse(value, out var b3)) s.ShowEmail = b3;
                    break;
                case "showphone":
                    if (bool.TryParse(value, out var b4)) s.ShowPhone = b4;
                    break;
                case "allowcvdownload":
                    if (bool.TryParse(value, out var b5)) s.AllowCvDownload = b5;
                    break;
                case "appearinsearch":
                    if (bool.TryParse(value, out var b6)) s.AppearInSearch = b6;
                    break;
                default:
                    break;
            }

            s.UpdatedAt = System.DateTime.UtcNow;
            _db.UserSettings.Update(s);
            await _db.SaveChangesAsync();
        }
    }
}
