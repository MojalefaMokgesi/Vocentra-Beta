namespace Vocentra.Models
{
    public static class PaymentStatuses
    {
        public const string PendingPayment = "PendingPayment";
        public const string AwaitingProof = "AwaitingProof";
        public const string UnderReview = "UnderReview";
        public const string Paid = "Paid";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";
    }
}
