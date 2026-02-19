using System.Linq;
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
    [Authorize(Roles = "Admin,FinanceAdmin")]
    public class PaymentsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPaymentWorkflowService _workflow;

        public PaymentsController(AppDbContext db, IPaymentWorkflowService workflow)
        {
            _db = db;
            _workflow = workflow;
        }

        public async Task<IActionResult> Queue(string status = "UnderReview")
        {
            var list = await _db.PaymentRequests
                .Include(p => p.Job)
                .Include(p => p.Messages)
                .Where(p => p.Status == PaymentStatus.UnderReview || p.Status == PaymentStatus.NeedsMoreInfo || p.Status == PaymentStatus.PendingPayment)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        public async Task<IActionResult> Review(int id)
        {
            var req = await _db.PaymentRequests
                .Include(p => p.Job)
                .Include(p => p.Submissions).ThenInclude(s => s.Document)
                .Include(p => p.Messages)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (req == null) return NotFound();
            return View(req);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _workflow.ApproveAsync(id, adminId);
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError("reason", "Reason is required");
                var req = await _db.PaymentRequests.FindAsync(id);
                return RedirectToAction(nameof(Review), new { id });
            }

            var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _workflow.DeclineAsync(id, adminId, reason);
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestMoreInfo(int id, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ModelState.AddModelError("message", "Message is required");
                return RedirectToAction(nameof(Review), new { id });
            }
            var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _workflow.RequestMoreInfoAsync(id, adminId, message);
            return RedirectToAction(nameof(Queue));
        }
    }
}
