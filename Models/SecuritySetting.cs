using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    public class SecuritySetting
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public bool TwoFactorEnabled { get; set; } = false;
        public bool LoginAlertsEnabled { get; set; } = true;
        public DateTime? LastPasswordChangeAt { get; set; }
        public bool ForceReauthAfterPasswordChange { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
