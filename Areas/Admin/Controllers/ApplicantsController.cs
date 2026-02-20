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
    public class ApplicantsController : Controller
    {
        private readonly AppDbContext _db;
        public ApplicantsController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var list = await _db.Applicants.Include(a => a.Job).OrderByDescending(a => a.Id).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var app = await _db.Applicants.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            return View(app);
        }
    }
}
