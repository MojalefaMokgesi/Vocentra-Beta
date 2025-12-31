using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    public class Applicant
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        // Personal Info
        public string? Title { get; set; }
        public string? Initials { get; set; }
        [Required] public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        [Required] public string? Surname { get; set; }
        public string? KnownAs { get; set; }
        [Required] public string? IdNumber { get; set; }
        public string? Nationality { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? HomeLanguage { get; set; }
        [Required] public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Gender { get; set; }
        public string? Ethnicity { get; set; }
        public string? Disability { get; set; }
        public string? HighestQualification { get; set; }
        public string? CurrentCTC { get; set; }
        public string? ExpectedCTC { get; set; }
        public string? CurrentLocation { get; set; }

        // Professional Links
        public string? LinkedInProfile { get; set; }
        public string? PortfolioWebsite { get; set; }
        [Required] public string? ResumeLink { get; set; }

        // Uploaded files
        public string? CertificateUrls { get; set; } // comma-separated URLs
        public string? DocumentUrls { get; set; }    // comma-separated URLs

        // Job relation
        public int? JobId { get; set; }
        public virtual Job? Job { get; set; }

        // Status
        public bool IsApplicationComplete { get; set; } = false;
    }
}
