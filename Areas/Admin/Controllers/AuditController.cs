using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly AppDbContext _db;
        public AuditController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var logs = await _db.AuditLogs.OrderByDescending(a => a.Timestamp).Take(200).ToListAsync();
            return View(logs);
        }
    }
}
