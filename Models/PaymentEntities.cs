using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Vocentra.Models
{
    public enum JobStatus
    {
        Draft = 0,
        PendingPayment = 1,
        PaymentDeclined = 2,
        Published = 3,
        Expired = 4
    }

    public enum PaymentStatus
    {
        PendingPayment = 0,
        UnderReview = 1,
        Approved = 2,
        Declined = 3,
        NeedsMoreInfo = 4
    }

    public enum DecisionType
    {
        Approve = 0,
        Decline = 1,
        NeedsMoreInfo = 2
    }

    public enum StorageProvider
    {
        Local = 0,
        AzureBlob = 1
    }

    public enum SenderRole
    {
        User = 0,
        Admin = 1
    }

    public class PaymentRequest
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string? UserId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        public PaymentStatus Status { get; set; } = PaymentStatus.PendingPayment;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public int? CurrentSubmissionId { get; set; }

        public string? RiskFlags { get; set; }

        public bool NeedsAttention { get; set; } = false;

        public Job? Job { get; set; }
        public ICollection<PaymentSubmission>? Submissions { get; set; }
        public ICollection<PaymentMessage>? Messages { get; set; }
    }

    public class PaymentSubmission
    {
        public int Id { get; set; }
        public int PaymentRequestId { get; set; }
        public string? SubmittedByUserId { get; set; }
        public int DocumentId { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public PaymentStatus SnapshotStatus { get; set; } = PaymentStatus.UnderReview;
        public string? FileHash { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountClaimed { get; set; }

        public PaymentRequest? PaymentRequest { get; set; }
        public ProofDocument? Document { get; set; }
    }

    public class ProofDocument
    {
        public int Id { get; set; }
        public StorageProvider StorageProvider { get; set; } = StorageProvider.Local;
        public string StoredPathOrBlobKey { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    public class PaymentBankAccount
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountHolder { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? BranchCode { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
        public DateTime? ValidTo { get; set; }
    }

    public class ModerationDecision
    {
        public int Id { get; set; }
        public int PaymentRequestId { get; set; }
        public string? AdminUserId { get; set; }
        public DecisionType DecisionType { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public PaymentRequest? PaymentRequest { get; set; }
    }

    public class PaymentMessage
    {
        public int Id { get; set; }
        public int PaymentRequestId { get; set; }
        public string? SenderUserId { get; set; }
        public SenderRole SenderRole { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public PaymentRequest? PaymentRequest { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? LinkUrl { get; set; }
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public string? ActorUserId { get; set; }
        public string? ActorRole { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? MetadataJson { get; set; }
    }
}
