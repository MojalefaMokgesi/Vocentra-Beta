using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    public class CompanyProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Location { get; set; }
        public string? LogoPath { get; set; }
        public string? About { get; set; }
        public string? ContactEmail { get; set; }
        public string? HiringManagerName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
