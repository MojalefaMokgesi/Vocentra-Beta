using Microsoft.AspNetCore.Identity;

namespace Vocentra.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CompanyName { get; set; }
        public string? AccountType { get; set; } // "Job Seeker" or "Recruiter"
    }
}
