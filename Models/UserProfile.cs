using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Vocentra.Models
{
    public class UserProfile
    {
        [Key]
        public int Id { get; set; }

        // Link to the logged-in user
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // Basic details
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        // Professional details
        public string? HighestQualification { get; set; }
        public string? ExperienceSummary { get; set; }
        public string? Skills { get; set; }

        // Optional CV upload
        public string? CvFilePath { get; set; }
    }
}
