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
        private readonly IWebHostEnvironment _env;

        public JobController(AppDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // GET: /Job
        public async Task<IActionResult> Index()
        {
            var jobs = await _context.Jobs.OrderByDescending(j => j.PostedAt).ToListAsync();
            return View(jobs);
        }

        // GET: /Job/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.Jobs.Include(j => j.Applicants)
                                         .FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();
            return View(job);
        }

        // GET: /Job/Apply/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Apply(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var applicant = await _context.Applicants
                .FirstOrDefaultAsync(a => a.UserId == userId && a.JobId == id);

            if (applicant == null)
            {
                applicant = new Applicant
                {
                    UserId = userId,
                    JobId = id
                };
            }

            return View(applicant);
        }

        // POST: /Job/Apply/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(Applicant model, IFormFile[] Certificates, IFormFile[] AdditionalDocuments)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);

            // Check if user already applied for THIS job
            var applicant = await _context.Applicants
                .FirstOrDefaultAsync(a => a.UserId == userId && a.JobId == model.JobId);

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

            // Upload root
            var uploadRoot = Path.Combine(_env.WebRootPath, "uploads");

            // Certificates
            if (Certificates != null && Certificates.Length > 0)
            {
                var certDir = Path.Combine(uploadRoot, "certificates");
                if (!Directory.Exists(certDir)) Directory.CreateDirectory(certDir);

                var certUrls = new List<string>();
                foreach (var file in Certificates)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var path = Path.Combine(certDir, fileName);
                    await using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);
                    certUrls.Add("/uploads/certificates/" + fileName);
                }
                applicant.CertificateUrls = string.Join(",", certUrls);
            }

            // Additional documents
            if (AdditionalDocuments != null && AdditionalDocuments.Length > 0)
            {
                var docDir = Path.Combine(uploadRoot, "documents");
                if (!Directory.Exists(docDir)) Directory.CreateDirectory(docDir);

                var docUrls = new List<string>();
                foreach (var file in AdditionalDocuments)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var path = Path.Combine(docDir, fileName);
                    await using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);
                    docUrls.Add("/uploads/documents/" + fileName);
                }
                applicant.DocumentUrls = string.Join(",", docUrls);
            }

            applicant.IsApplicationComplete = true;
            applicant.AppliedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction("ThankYou");
        }

        // GET: /Job/ThankYou
        [Authorize]
        public IActionResult ThankYou()
        {
            return View();
        }
    }
}
