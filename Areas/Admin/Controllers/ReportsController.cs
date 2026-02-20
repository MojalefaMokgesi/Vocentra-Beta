using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _db;
        public ReportsController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var jobs7 = await _db.Jobs.CountAsync(j => j.PostedAt >= now.AddDays(-7));
            var jobs30 = await _db.Jobs.CountAsync(j => j.PostedAt >= now.AddDays(-30));

            var paymentsApproved7 = await _db.PaymentRequests.CountAsync(p => p.Status == PaymentStatus.Approved && p.UpdatedAt >= now.AddDays(-7));
            var paymentsApproved30 = await _db.PaymentRequests.CountAsync(p => p.Status == PaymentStatus.Approved && p.UpdatedAt >= now.AddDays(-30));

            var model = new {
                Jobs7 = jobs7,
                Jobs30 = jobs30,
                PaymentsApproved7 = paymentsApproved7,
                PaymentsApproved30 = paymentsApproved30
            };

            return View(model);
        }
    }
}
