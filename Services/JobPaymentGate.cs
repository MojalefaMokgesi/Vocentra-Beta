using Vocentra.Models;

namespace Vocentra.Services
{
    public static class JobPaymentGate
    {
        public static bool CanActivate(Job job)
            => job != null && string.Equals(job.PaymentStatus, PaymentStatuses.Paid, System.StringComparison.OrdinalIgnoreCase);

        public static void Apply(Job job)
        {
            if (CanActivate(job))
            {
                job.IsPaid = true;
                job.Status = "Active";
            }
            else
            {
                job.IsPaid = false;
                if (!string.Equals(job.Status, "Expired", System.StringComparison.OrdinalIgnoreCase))
                    job.Status = "Draft";
            }
        }
    }
}
