using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace Vocentra.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string JobType { get; set; } = string.Empty;

        [Required, Column(TypeName = "decimal(18,2)")]
        [DataType(DataType.Currency)]
        public decimal Salary { get; set; }

        public DateTime PostedAt { get; set; } = DateTime.Now;

        [Required, MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ExperienceLevel { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? ApplicationDeadline { get; set; }

        public string Benefits { get; set; } = string.Empty;

        public string SkillsRequired { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        public string? CompanyLogoUrl { get; set; }

        [NotMapped] // EF ignores this property
        public IFormFile? CompanyLogoFile { get; set; }

        public ICollection<Applicant>? Applicants { get; set; }
    }
}
