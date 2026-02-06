using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    // Notification and privacy preferences for a user
    public class UserSetting
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // FK to AspNetUsers.Id

        public bool EmailNotifications { get; set; } = true;
        public bool JobAlerts { get; set; } = true;
        public string? AlertFrequency { get; set; }
        public string? AlertCategories { get; set; }
        public string? AlertLocations { get; set; }
        public string? PreferredContactMethod { get; set; }
        public string? ProfileVisibility { get; set; }
        public bool ShowEmail { get; set; } = false;
        public bool ShowPhone { get; set; } = false;
        public bool AllowCvDownload { get; set; } = true;
        public bool AppearInSearch { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
