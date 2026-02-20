using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class JobsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;

        public JobsController(AppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<IActionResult> Queue()
        {
            var list = await _db.Jobs
                .Include(j => j.Applicants)
                .Where(j => j.JobStatus == JobStatus.PendingPayment || j.JobStatus == JobStatus.Draft || j.JobStatus == JobStatus.PaymentDeclined)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Review(int id)
        {
            var job = await _db.Jobs.Include(j => j.Applicants).FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();
            return View(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var job = await _db.Jobs.FindAsync(id);
            if (job == null) return NotFound();
            job.JobStatus = JobStatus.Published;
            job.PublishedAt = System.DateTime.UtcNow;
            job.ApprovedByUserId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _db.Jobs.Update(job);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(job.ApprovedByUserId ?? "", "Admin", "PublishJob", nameof(Job), job.Id.ToString());
            TempData["Success"] = "Job published.";
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Reason is required to reject a job.";
                return RedirectToAction(nameof(Review), new { id });
            }

            var job = await _db.Jobs.FindAsync(id);
            if (job == null) return NotFound();
            job.JobStatus = JobStatus.PaymentDeclined;
            _db.Jobs.Update(job);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(User?.Identity?.Name ?? string.Empty, "Admin", "RejectJob", nameof(Job), job.Id.ToString(), metadataJson: reason);
            TempData["Success"] = "Job rejected.";
            return RedirectToAction(nameof(Queue));
        }
    }
}
