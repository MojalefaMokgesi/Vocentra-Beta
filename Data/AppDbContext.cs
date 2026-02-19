using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Vocentra.Models;

namespace Vocentra.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Applicant> Applicants => Set<Applicant>();

        // Settings
        public DbSet<Vocentra.Models.Setting> Settings => Set<Vocentra.Models.Setting>();
        public DbSet<Vocentra.Models.UserSetting> UserSettings => Set<Vocentra.Models.UserSetting>();
        public DbSet<Vocentra.Models.UserProfile> UserProfiles => Set<Vocentra.Models.UserProfile>();
        public DbSet<Vocentra.Models.CompanyProfile> CompanyProfiles => Set<Vocentra.Models.CompanyProfile>();
        public DbSet<Vocentra.Models.SecuritySetting> SecuritySettings => Set<Vocentra.Models.SecuritySetting>();
        public DbSet<Vocentra.Models.SecurityActivityLog> SecurityActivityLogs => Set<Vocentra.Models.SecurityActivityLog>();

        // NEW tables for one-click apply system
        public DbSet<UserApplicationProfile> UserApplicationProfiles => Set<UserApplicationProfile>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        // Payment proof workflow
        public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
        public DbSet<PaymentSubmission> PaymentSubmissions => Set<PaymentSubmission>();
        public DbSet<ProofDocument> ProofDocuments => Set<ProofDocument>();
        public DbSet<PaymentBankAccount> PaymentBankAccounts => Set<PaymentBankAccount>();
        public DbSet<ModerationDecision> ModerationDecisions => Set<ModerationDecision>();
        public DbSet<PaymentMessage> PaymentMessages => Set<PaymentMessage>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PaymentRequest>()
                .HasOne(pr => pr.Job)
                .WithMany()
                .HasForeignKey(pr => pr.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentSubmission>()
                .HasOne(s => s.PaymentRequest)
                .WithMany(r => r.Submissions)
                .HasForeignKey(s => s.PaymentRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentSubmission>()
                .HasOne(s => s.Document)
                .WithMany()
                .HasForeignKey(s => s.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentMessage>()
                .HasOne(m => m.PaymentRequest)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.PaymentRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
