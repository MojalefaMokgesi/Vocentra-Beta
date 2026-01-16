using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public JobController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // ===================== DETAILS =====================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.Jobs
                .Include(j => j.Applicants)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            return View(job);
        }

        // ===================== APPLY (GET) =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Apply(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            ViewBag.JobTitle = job.Title;

            var userId = _userManager.GetUserId(User);

            var applicant = await _context.Applicants
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.JobId == id);

            // Already applied → block re-apply
            if (applicant != null && applicant.IsApplicationComplete)
                return RedirectToAction(nameof(Details), new { id });

            return View(applicant ?? new Applicant { JobId = id });
        }

        // ===================== APPLY (POST) =====================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(Applicant model, int CurrentStep = 1)
        {
            var userId = _userManager.GetUserId(User);

            // Prevent ModelState failing because these are not posted from the form
            ModelState.Remove(nameof(Applicant.UserId));
            ModelState.Remove(nameof(Applicant.User));
            ModelState.Remove(nameof(Applicant.Job));
            ModelState.Remove(nameof(Applicant.Id));
            ModelState.Remove(nameof(Applicant.AppliedAt));
            ModelState.Remove(nameof(Applicant.CertificateUrls));
            ModelState.Remove(nameof(Applicant.DocumentUrls));
            ModelState.Remove(nameof(Applicant.IsApplicationComplete));

            // If invalid, return to the same step
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                Console.WriteLine("ModelState errors: " + errors);

                var job = await _context.Jobs.FindAsync(model.JobId);
                ViewBag.JobTitle = job?.Title ?? "Job";
                ViewBag.StartStep = CurrentStep;
                return View(model);
            }

            // Ensure Job exists
            var jobExists = await _context.Jobs.AnyAsync(j => j.Id == model.JobId);
            if (!jobExists) return NotFound();

            // Check if applicant already exists
            var applicant = await _context.Applicants
                .FirstOrDefaultAsync(a => a.UserId == userId && a.JobId == model.JobId);

            if (applicant == null)
            {
                applicant = new Applicant
                {
                    UserId = userId!,
                    JobId = model.JobId,
                    AppliedAt = DateTime.UtcNow
                };
                _context.Applicants.Add(applicant);
            }

            // Map fields
            applicant.Title = model.Title;
            applicant.Initials = model.Initials;
            applicant.FirstName = model.FirstName;
            applicant.MiddleName = model.MiddleName;
            applicant.Surname = model.Surname;
            applicant.KnownAs = model.KnownAs;
            applicant.IdNumber = model.IdNumber;
            applicant.Nationality = model.Nationality;
            applicant.DateOfBirth = model.DateOfBirth;
            applicant.HomeLanguage = model.HomeLanguage;
            applicant.Email = model.Email;
            applicant.Telephone = model.Telephone;
            applicant.Gender = model.Gender;
            applicant.Ethnicity = model.Ethnicity;
            applicant.Disability = model.Disability;
            applicant.HighestQualification = model.HighestQualification;
            applicant.CurrentCTC = model.CurrentCTC;
            applicant.ExpectedCTC = model.ExpectedCTC;
            applicant.CurrentLocation = model.CurrentLocation;
            applicant.ResumeLink = model.ResumeLink;
            applicant.LinkedInProfile = model.LinkedInProfile;
            applicant.PortfolioWebsite = model.PortfolioWebsite;
            applicant.IsApplicationComplete = true;

            // ===== FILE UPLOADS =====
            var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "applicants");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

            // Certificates
            if (model.CertificatesFiles != null && model.CertificatesFiles.Count > 0)
            {
                var certUrls = new List<string>();
                foreach (var file in model.CertificatesFiles)
                {
                    if (file.Length == 0) continue;

                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var path = Path.Combine(uploadFolder, fileName);

                    await using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);

                    certUrls.Add(fileName);
                }
                applicant.CertificateUrls = string.Join(",", certUrls);
            }

            // Additional Documents
            if (model.AdditionalDocumentsFiles != null && model.AdditionalDocumentsFiles.Count > 0)
            {
                var docUrls = new List<string>();
                foreach (var file in model.AdditionalDocumentsFiles)
                {
                    if (file.Length == 0) continue;

                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var path = Path.Combine(uploadFolder, fileName);

                    await using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);

                    docUrls.Add(fileName);
                }
                applicant.DocumentUrls = string.Join(",", docUrls);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = model.JobId });
        }
    }
}
