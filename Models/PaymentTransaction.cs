using System;
using System.ComponentModel.DataAnnotations;

namespace Vocentra.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }

        // Navigation (this is what your controller needs)
        public Job? Job { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public decimal AmountZar { get; set; }

        public int DaysPaid { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Required]
        public string MerchantReference { get; set; } = string.Empty;

        [Required]
        public string Provider { get; set; } = "ManualEFT";

        [Required]
        public string Status { get; set; } = PaymentStatuses.PendingPayment;

        // Manual EFT proof
        public string? ProofFilePath { get; set; }
        public string? UserBankName { get; set; }
        public string? UserPaymentReference { get; set; }
        public DateTime? ProofSubmittedAtUtc { get; set; }

        // Admin review
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewNote { get; set; }

        // Paid
        public DateTime? PaidAtUtc { get; set; }

        // Tracking
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
