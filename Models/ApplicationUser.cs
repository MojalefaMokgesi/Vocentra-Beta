using Microsoft.AspNetCore.Identity;

namespace Vocentra.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CompanyName { get; set; }
        public bool IsDeactivated { get; set; }
        // When a user requests an email change we store the pending value and timestamp
        public string? PendingEmail { get; set; }
        public DateTime? PendingEmailRequestedAt { get; set; }
        public DateTime? LastPasswordChangedAt { get; set; }
    }
}
