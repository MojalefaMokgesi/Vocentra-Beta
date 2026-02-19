using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Services
{
    public class PaymentWorkflowService : IPaymentWorkflowService
    {
        private readonly AppDbContext _db;
        private readonly IProofStorageService _storage;
        private readonly INotificationService _notifier;
        private readonly IAuditService _audit;
        private readonly ILogger<PaymentWorkflowService> _log;

        public PaymentWorkflowService(AppDbContext db, IProofStorageService storage, INotificationService notifier, IAuditService audit, ILogger<PaymentWorkflowService> log)
        {
            _db = db;
            _storage = storage;
            _notifier = notifier;
            _audit = audit;
            _log = log;
        }

        public async Task<PaymentRequest> CreatePaymentRequestAsync(Job job)
        {
            var pr = new PaymentRequest
            {
                JobId = job.Id,
                UserId = job.OwnerUserId,
                Amount = job.PriceZar,
                Reference = job.PaymentReference ?? Guid.NewGuid().ToString(),
                Status = PaymentStatus.PendingPayment,
                CreatedAt = DateTime.UtcNow
            };
            _db.PaymentRequests.Add(pr);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(job.OwnerUserId, "User", "CreatePaymentRequest", nameof(PaymentRequest), pr.Id.ToString());
            return pr;
        }

        public async Task<PaymentSubmission> SubmitProofAsync(int requestId, IFormFile file, string? notes, string? userId, decimal? amountClaimed = null)
        {
            var req = await _db.PaymentRequests.Include(r => r.Submissions).FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) throw new InvalidOperationException("PaymentRequest not found");

            // basic validation
            if (file == null || file.Length == 0) throw new InvalidOperationException("File required");
            if (file.Length > 10 * 1024 * 1024) throw new InvalidOperationException("File too large");

            // magic bytes check (pdf/png/jpg)
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();
            if (!IsAllowedFile(data, file.ContentType)) throw new InvalidOperationException("Invalid file type");

            // compute hash
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = Convert.ToHexString(sha.ComputeHash(data));
            }

            // duplicate detection
            var existing = await _db.ProofDocuments.FirstOrDefaultAsync(d => d.Sha256 == hash);

            ProofDocument doc;
            if (existing != null)
            {
                doc = existing;
            }
            else
            {
                // save to storage
                // recreate IFormFile from memory
                ms.Position = 0;
                var tmpFile = new FormFile(ms, 0, data.Length, file.Name, file.FileName)
                {
                    Headers = file.Headers,
                    ContentType = file.ContentType
                };

                doc = await _storage.SaveAsync(tmpFile);
                _db.ProofDocuments.Add(doc);
                await _db.SaveChangesAsync();
            }

            var submission = new PaymentSubmission
            {
                PaymentRequestId = req.Id,
                SubmittedByUserId = userId,
                DocumentId = doc.Id,
                SubmittedAt = DateTime.UtcNow,
                Notes = notes,
                SnapshotStatus = PaymentStatus.UnderReview,
                FileHash = hash,
                AmountClaimed = amountClaimed
            };
            _db.PaymentSubmissions.Add(submission);
            req.Status = PaymentStatus.UnderReview;
            req.UpdatedAt = DateTime.UtcNow;
            req.CurrentSubmissionId = submission.Id;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(userId, "User", "SubmitProof", nameof(PaymentSubmission), submission.Id.ToString());
            await _notifier.CreateNotificationAsync(req.UserId ?? string.Empty, "Payment proof received", $"Your payment proof for job {req.JobId} was received and is under review.");

            return submission;
        }

        public async Task ApproveAsync(int requestId, string adminUserId)
        {
            var req = await _db.PaymentRequests.Include(r => r.Job).FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) throw new InvalidOperationException("Request not found");

            req.Status = PaymentStatus.Approved;
            req.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // publish job
            var job = req.Job;
            if (job != null)
            {
                job.JobStatus = JobStatus.Published;
                job.PublishedAt = DateTime.UtcNow;
                job.ApprovedByUserId = adminUserId;
                job.IsPaid = true;
                await _db.SaveChangesAsync();
            }

            await _audit.LogAsync(adminUserId, "Admin", "ApprovePayment", nameof(PaymentRequest), req.Id.ToString());
            await _notifier.CreateNotificationAsync(req.UserId ?? string.Empty, "Payment approved", $"Your payment for job {req.JobId} has been approved and the job is published.");
        }

        public async Task DeclineAsync(int requestId, string adminUserId, string reason)
        {
            var req = await _db.PaymentRequests.Include(r => r.Job).FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) throw new InvalidOperationException("Request not found");

            req.Status = PaymentStatus.Declined;
            req.UpdatedAt = DateTime.UtcNow;
            req.NeedsAttention = true;
            _db.ModerationDecisions.Add(new ModerationDecision { PaymentRequestId = req.Id, AdminUserId = adminUserId, DecisionType = DecisionType.Decline, Reason = reason });
            if (req.Job != null)
            {
                req.Job.JobStatus = JobStatus.PaymentDeclined;
            }
            await _db.SaveChangesAsync();

            await _audit.LogAsync(adminUserId, "Admin", "DeclinePayment", nameof(PaymentRequest), req.Id.ToString(), metadataJson: reason);
            await _notifier.CreateNotificationAsync(req.UserId ?? string.Empty, "Payment declined", $"Your payment for job {req.JobId} has been declined: {reason}");
        }

        public async Task RequestMoreInfoAsync(int requestId, string adminUserId, string message)
        {
            var req = await _db.PaymentRequests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) throw new InvalidOperationException("Request not found");

            req.Status = PaymentStatus.NeedsMoreInfo;
            req.UpdatedAt = DateTime.UtcNow;
            req.NeedsAttention = true;
            _db.PaymentMessages.Add(new PaymentMessage { PaymentRequestId = req.Id, SenderUserId = adminUserId, SenderRole = SenderRole.Admin, Message = message });
            await _db.SaveChangesAsync();

            await _audit.LogAsync(adminUserId, "Admin", "RequestMoreInfo", nameof(PaymentRequest), req.Id.ToString(), metadataJson: message);
            await _notifier.CreateNotificationAsync(req.UserId ?? string.Empty, "Payment requires more info", message);
        }

        private bool IsAllowedFile(byte[] data, string contentType)
        {
            if (contentType == "application/pdf" && data.Length > 4 && data[0] == 0x25 && data[1] == 0x50) return true; // %PDF
            if ((contentType == "image/png" || contentType == "image/x-png") && data.Length > 8 && data[0] == 0x89 && data[1] == 0x50) return true; // PNG
            if ((contentType == "image/jpeg" || contentType == "image/jpg") && data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8) return true; // JPG
            // fallback by extension/contentType
            var allowed = new[] { "application/pdf", "image/png", "image/jpeg" };
            return allowed.Contains(contentType);
        }
    }
}
