using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // ? Needed for [Authorize]
using Vocentra.Data;
using Vocentra.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Vocentra.Controllers
{
    [Authorize] // ? Require authentication for all actions in this controller
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // Index action for job listings
        public async Task<IActionResult> Index(string? title, string? location, string? jobType)
        {
            ViewBag.FilterTitle = title ?? "";
            ViewBag.FilterLocation = location ?? "";
            ViewBag.FilterJobType = jobType ?? "";

            var jobsQuery = _context.Jobs.AsQueryable();

            if (!string.IsNullOrEmpty(title))
                jobsQuery = jobsQuery.Where(j => j.Title.Contains(title));

            if (!string.IsNullOrEmpty(location))
                jobsQuery = jobsQuery.Where(j => j.Location.Contains(location));

            if (!string.IsNullOrEmpty(jobType))
                jobsQuery = jobsQuery.Where(j => j.JobType.Contains(jobType));

            var jobs = await jobsQuery
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            return View(jobs);
        }

        // Privacy page
        public IActionResult Privacy()
        {
            return View(); // Views/Home/Privacy.cshtml
        }
    }
}
