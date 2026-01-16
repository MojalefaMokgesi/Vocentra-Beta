using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace Vocentra.Models
{
    public class Applicant
    {
        public int Id { get; set; }

        // =========================
        // Identity / Relations
        // =========================

        [Required]
        public string UserId { get; set; } = null!; // FK -> AspNetUsers.Id (string)

        [Required]
        public int JobId { get; set; }               // FK -> Jobs.Id (int)

        public virtual ApplicationUser? User { get; set; }
        public virtual Job? Job { get; set; }

        // =========================
        // Personal Information
        // =========================

        public string? Title { get; set; }
        public string? Initials { get; set; }

        [Required]
        public string FirstName { get; set; } = null!;

        public string? MiddleName { get; set; }

        [Required]
        public string Surname { get; set; } = null!;

        public string? KnownAs { get; set; }

        [Required]
        public string IdNumber { get; set; } = null!;

        public string? Nationality { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? HomeLanguage { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        public string? Telephone { get; set; }
        public string? Gender { get; set; }
        public string? Ethnicity { get; set; }
        public string? Disability { get; set; }

        public string? HighestQualification { get; set; }
        public string? CurrentCTC { get; set; }
        public string? ExpectedCTC { get; set; }
        public string? CurrentLocation { get; set; }

        // =========================
        // Professional Links
        // =========================

        public string? LinkedInProfile { get; set; }
        public string? PortfolioWebsite { get; set; }
        public string? ResumeLink { get; set; }

        // =========================
        // Uploaded Documents (DB)
        // =========================

        public string? CertificateUrls { get; set; } // comma-separated
        public string? DocumentUrls { get; set; }    // comma-separated

        // =========================
        // Uploaded Files (NOT MAPPED)
        // =========================

        [NotMapped]
        public List<IFormFile>? CertificatesFiles { get; set; }

        [NotMapped]
        public List<IFormFile>? AdditionalDocumentsFiles { get; set; }

        // =========================
        // Status / Tracking
        // =========================

        public bool IsApplicationComplete { get; set; } = false;

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}
