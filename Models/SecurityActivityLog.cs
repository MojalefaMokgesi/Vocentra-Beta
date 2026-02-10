using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    public class SecurityActivityLog
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
