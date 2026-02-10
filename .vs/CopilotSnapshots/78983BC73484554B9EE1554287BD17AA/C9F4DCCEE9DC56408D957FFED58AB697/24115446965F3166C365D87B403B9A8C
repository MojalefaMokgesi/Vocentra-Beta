using System.ComponentModel.DataAnnotations;

namespace Vocentra.ViewModels
{
    public class AccountSettingsViewModel
    {
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        public string? AccountType { get; set; }
    }
}
