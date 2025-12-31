using System;

namespace Vocentra.Models
{
    public class Application
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int JobId { get; set; }       // FK to Job (must match DB)
        public string UserId { get; set; }    // FK to AspNetUsers

        public string ProfileSnapshotJson { get; set; }
        public string CvPath { get; set; }

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    }


    public enum ApplicationStatus
    {
        Submitted,
        Reviewed,
        Shortlisted,
        Rejected,
        Hired
    }
}
