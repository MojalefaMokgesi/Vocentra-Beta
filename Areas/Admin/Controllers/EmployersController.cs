using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EmployersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployersController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var owners = await _db.Jobs
                .GroupBy(j => j.OwnerUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var model = new System.Collections.Generic.List<dynamic>();
            foreach (var o in owners)
            {
                var user = o.UserId != null ? await _userManager.FindByIdAsync(o.UserId) : null;
                model.Add(new { User = user, JobCount = o.Count });
            }

            return View(model);
        }
    }
}
