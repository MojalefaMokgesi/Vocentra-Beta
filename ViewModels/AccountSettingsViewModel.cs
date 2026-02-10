using System.ComponentModel.DataAnnotations;

namespace Vocentra.ViewModels
{
    public class AccountSettingsViewModel
    {
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [EmailAddress]
        public string? NewEmail { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        [RegularExpression(@"^(\+27|0)\d{9}$", ErrorMessage = "Enter a valid South African phone number (e.g. 0821234567 or +27821234567)")]
        public string? PhoneNumber { get; set; }

        // Notification preferences
        public bool EmailNotifications { get; set; }
        public bool JobAlerts { get; set; }

        // Computed
        public int ProfileCompletion { get; set; }
        public bool EmailVerified { get; set; }
        // Server-side flags
        public bool EmailChangeRequested { get; set; }
    }
}
