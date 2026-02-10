using System;


namespace Vocentra.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }


        public int JobId { get; set; }
        public string UserId { get; set; } = null!;


        // LOCKED pricing at pay-time (prevents midnight mismatches)
        public decimal AmountZar { get; set; }
        public int DaysPaid { get; set; }
        public DateTime StartDate { get; set; } // Date used to compute pricing (Date-only in practice)
        public DateTime EndDate { get; set; } // Job deadline date


        public string Status { get; set; } = "Pending"; // Pending / Paid / Failed
        public string Provider { get; set; } = "PayFast";


        public string MerchantReference { get; set; } = null!; // unique
        public string? ProviderPaymentId { get; set; } // pf_payment_id


        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAtUtc { get; set; }


        public Job Job { get; set; } = null!;
    }
}