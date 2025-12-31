using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Controllers
{
    public class JobController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JobController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Job
        public async Task<IActionResult> Index()
        {
            var jobs = await _context.Jobs
                .OrderByDescending(j => j.PostedAt) // Changed from PostedDate to PostedAt
                .ToListAsync();
            return View(jobs);
        }

        // GET: /Job/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.Jobs
                .Include(j => j.Applicants) // Optional: Include applicants if needed
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            return View(job);
        }

        // GET: /Job/Apply/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Apply(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            var applicant = await _context.Applicants.FirstOrDefaultAsync(a => a.UserId == userId);

            if (applicant == null)
            {
                applicant = new Applicant
                {
                    UserId = userId,
                    JobId = id
                };
            }
            else
            {
                applicant.JobId = id;
            }

            return View(applicant);
        }

        // POST: /Job/Apply/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(Applicant model, IFormFile[] Certificates, IFormFile[] AdditionalDocuments)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = _userManager.GetUserId(User);
            var applicant = await _context.Applicants.FirstOrDefaultAsync(a => a.UserId == userId);

            if (applicant == null)
            {
                applicant = new Applicant
                {
                    UserId = userId,
                    JobId = model.JobId
                };
                _context.Applicants.Add(applicant);
            }

            // Update applicant details
            applicant.Title = model.Title;
            applicant.FirstName = model.FirstName;
            applicant.MiddleName = model.MiddleName;
            applicant.Surname = model.Surname;
            applicant.IdNumber = model.IdNumber;
            applicant.Email = model.Email;
            applicant.CurrentLocation = model.CurrentLocation;
            applicant.ResumeLink = model.ResumeLink;

            // Handle Certificates
            if (Certificates != null && Certificates.Length > 0)
            {
                var certUrls = new List<string>();
                foreach (var file in Certificates)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var path = Path.Combine("wwwroot/uploads/certificates", fileName);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    certUrls.Add("/uploads/certificates/" + fileName);
                }
                applicant.CertificateUrls = string.Join(",", certUrls);
            }

            // Handle Additional Documents
            if (AdditionalDocuments != null && AdditionalDocuments.Length > 0)
            {
                var docUrls = new List<string>();
                foreach (var file in AdditionalDocuments)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var path = Path.Combine("wwwroot/uploads/documents", fileName);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    docUrls.Add("/uploads/documents/" + fileName);
                }
                applicant.DocumentUrls = string.Join(",", docUrls);
            }

            applicant.IsApplicationComplete = true;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = applicant.JobId });
        }
    }
}
