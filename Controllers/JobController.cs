using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;

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

            if (!job.IsPaid || !string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var userId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                if (!isAdmin && job.OwnerUserId != userId)
                    return NotFound();
            }

            return View(job);
        }

        // ===================== CREATE =====================
        [Authorize]
        [HttpGet]
        public IActionResult Create() => View();

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Job job)
        {
            if (!ModelState.IsValid)
                return View(job);

            if (!job.ApplicationDeadline.HasValue)
            {
                ModelState.AddModelError(nameof(Job.ApplicationDeadline), "Application deadline is required.");
                return View(job);
            }

            if (job.CompanyLogoFile != null && job.CompanyLogoFile.Length > 0)
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "company-logos");
                Directory.CreateDirectory(dir);

                var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(job.CompanyLogoFile.FileName)}";
                var path = Path.Combine(dir, fileName);

                await using var stream = new FileStream(path, FileMode.Create);
                await job.CompanyLogoFile.CopyToAsync(stream);

                job.CompanyLogoUrl = $"/uploads/company-logos/{fileName}";
            }

            job.PostedAt = DateTime.UtcNow;
            job.OwnerUserId = _userManager.GetUserId(User)!;
            job.IsPaid = false;
            job.Status = "Draft";
            job.PaymentStatus = "Pending";
            job.PriceZar = Pricing.Price(DateTime.UtcNow, job.ApplicationDeadline.Value);

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Publish), new { id = job.Id });
        }

        // ===================== PUBLISH =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Publish(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && job.OwnerUserId != userId)
                return Forbid();

            var start = DateTime.UtcNow.Date;
            var end = job.ApplicationDeadline?.Date ?? start;

            ViewBag.Days = Pricing.DaysInclusive(start, end);
            ViewBag.Amount = ViewBag.Days * Pricing.RatePerDayZar;

            return View(job);
        }

        // ===================== APPLY (GET) =====================
        [Authorize]
        [HttpGet("Job/Apply/{id:int}")]
        public async Task<IActionResult> Apply(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            if (!job.IsPaid || !string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            ViewBag.JobTitle = job.Title;

            var userId = _userManager.GetUserId(User);

            var applicant = await _context.Applicants
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId && a.JobId == id);

            if (applicant != null && applicant.IsApplicationComplete)
                return RedirectToAction(nameof(Details), new { id });

            return View(applicant ?? new Applicant { JobId = id });
        }

        // ===================== APPLY (POST) =====================
        [Authorize]
        [HttpPost("Job/Apply/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int id, Applicant model, int CurrentStep = 1)
        {
            var userId = _userManager.GetUserId(User);
            model.JobId = id;

            ModelState.Remove(nameof(Applicant.UserId));
            ModelState.Remove(nameof(Applicant.User));
            ModelState.Remove(nameof(Applicant.Job));
            ModelState.Remove(nameof(Applicant.Id));
            ModelState.Remove(nameof(Applicant.AppliedAt));
            ModelState.Remove(nameof(Applicant.CertificateUrls));
            ModelState.Remove(nameof(Applicant.DocumentUrls));
            ModelState.Remove(nameof(Applicant.IsApplicationComplete));

            if (!ModelState.IsValid)
            {
                var job = await _context.Jobs.FindAsync(id);
                ViewBag.JobTitle = job?.Title ?? "Job";
                ViewBag.StartStep = CurrentStep;
                return View(model);
            }

            var jobEntity = await _context.Jobs.FindAsync(id);
            if (jobEntity == null ||
                !jobEntity.IsPaid ||
                !string.Equals(jobEntity.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var applicant = await _context.Applicants
                .FirstOrDefaultAsync(a => a.UserId == userId && a.JobId == id);

            if (applicant == null)
            {
                applicant = new Applicant
                {
                    UserId = userId!,
                    JobId = id,
                    AppliedAt = DateTime.UtcNow
                };
                _context.Applicants.Add(applicant);
            }

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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ===================== MY JOBS =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var jobs = await _context.Jobs
                .Where(j => isAdmin || j.OwnerUserId == userId)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            return View(jobs);
        }

        // ===================== DELETE =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && job.OwnerUserId != userId)
                return Forbid();

            return View(job);
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && job.OwnerUserId != userId)
                return Forbid();

            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(My));
        }
    }
}
