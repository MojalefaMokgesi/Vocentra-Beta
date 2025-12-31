using System;

namespace Vocentra.Models
{
    public class UserApplicationProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } // FK to AspNetUsers

        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        // JSON fields for flexible data
        public string ExperienceJson { get; set; }
        public string EducationJson { get; set; }
        public string SkillsJson { get; set; }
        public string OtherFieldsJson { get; set; }

        // File paths for CV + Cover Letter
        public string ProfileCvPath { get; set; }
        public string CoverLetterPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
