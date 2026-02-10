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

            // Gate draft/unpaid from public
            if (!job.IsPaid || !string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var userId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                if (!isAdmin && !string.Equals(job.OwnerUserId, userId, StringComparison.Ordinal))
                    return NotFound();
            }

            return View(job);
        }

        // ===================== CREATE (GET) =====================
        [Authorize]
        [HttpGet]
        public IActionResult Create() => View();

        // ===================== CREATE (POST) =====================
        // Saves as Draft (unpaid), then redirects to Publish (price + PayFast button)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Job job)
        {
            if (!ModelState.IsValid) return View(job);

            // Deadline is REQUIRED for pricing, and yours is nullable DateTime?
            if (!job.ApplicationDeadline.HasValue)
            {
                ModelState.AddModelError(nameof(Job.ApplicationDeadline), "Application deadline is required.");
                return View(job);
            }

            // Handle file upload
            if (job.CompanyLogoFile != null && job.CompanyLogoFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "company-logos");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                // Avoid collisions by prefixing Guid
                var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(job.CompanyLogoFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await job.CompanyLogoFile.CopyToAsync(stream);

                job.CompanyLogoUrl = $"/uploads/company-logos/{fileName}";
            }

            job.PostedAt = DateTime.UtcNow;
            job.OwnerUserId = _userManager.GetUserId(User) ?? string.Empty;

            // Force Draft/unpaid until PayFast ITN confirms
            job.IsPaid = false;
            job.Status = "Draft";
            job.PaymentStatus = "Pending";
            job.PaidAt = null;
            job.PaidUntil = null;

            // Optional: set a display-only price (source of truth is Pricing at pay-time in PaymentsController)
            job.PriceZar = Pricing.Price(DateTime.UtcNow, job.ApplicationDeadline.Value);

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            // Option B: go to Publish page (price + PayFast)
            return RedirectToAction(nameof(Publish), new { id = job.Id });
        }

        // ===================== PUBLISH (GET) =====================
        // Shows price breakdown and PayFast button.
        // You must create Views/Job/Publish.cshtml.
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Publish(int id)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && !string.Equals(job.OwnerUserId, userId, StringComparison.Ordinal))
                return Forbid();

            if (!job.ApplicationDeadline.HasValue)
            {
                // Shouldn’t happen if Create validates, but safe.
                ViewBag.Days = 0;
                ViewBag.Amount = 0m;
                return View(job);
            }

            var start = DateTime.UtcNow.Date;
            var end = job.ApplicationDeadline.Value.Date;

            var days = Pricing.DaysInclusive(start, end);
            var amount = days <= 0 ? 0m : (days * Pricing.RatePerDayZar);

            ViewBag.Days = days;
            ViewBag.Amount = amount;

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

            // Block applying to unpaid/inactive jobs
            if (!job.IsPaid || !string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
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

            // Ensure Job exists AND is active/paid
            var jobEntity = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == model.JobId);
            if (jobEntity == null) return NotFound();
            if (!jobEntity.IsPaid || !string.Equals(jobEntity.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return NotFound();

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

        // ===================== MY JOBS =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var jobsQuery = _context.Jobs.AsQueryable();
            if (!isAdmin)
                jobsQuery = jobsQuery.Where(j => j.OwnerUserId == userId);

            var jobs = await jobsQuery.OrderByDescending(j => j.PostedAt).ToListAsync();
            return View(jobs);
        }

        // ===================== EDIT =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && job.OwnerUserId != userId)
                return Forbid();

            return View(job);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Job job)
        {
            if (id != job.Id) return NotFound();
            if (!ModelState.IsValid) return View(job);

            var existing = await _context.Jobs.FindAsync(id);
            if (existing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && existing.OwnerUserId != userId)
                return Forbid();

            try
            {
                // Handle new file upload
                if (job.CompanyLogoFile != null && job.CompanyLogoFile.Length > 0)
                {
                    var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "company-logos");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(job.CompanyLogoFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);

                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await job.CompanyLogoFile.CopyToAsync(stream);

                    existing.CompanyLogoUrl = $"/uploads/company-logos/{fileName}";
                }

                // Map editable fields
                existing.Title = job.Title;
                existing.Description = job.Description;
                existing.Location = job.Location;
                existing.Salary = job.Salary;
                existing.JobType = job.JobType;
                existing.Category = job.Category;
                existing.ExperienceLevel = job.ExperienceLevel;
                existing.ApplicationDeadline = job.ApplicationDeadline;
                existing.Benefits = job.Benefits;
                existing.SkillsRequired = job.SkillsRequired;
                existing.CompanyName = job.CompanyName;

                // Refresh display-only price if still unpaid/draft
                if ((!existing.IsPaid) || !string.Equals(existing.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing.ApplicationDeadline.HasValue)
                        existing.PriceZar = Pricing.Price(DateTime.UtcNow, existing.ApplicationDeadline.Value);
                }

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(My));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Jobs.Any(j => j.Id == job.Id)) return NotFound();
                throw;
            }
        }

        // ===================== DELETE =====================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null) return NotFound();

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
            if (job == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && job.OwnerUserId != userId)
                return Forbid();

            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(My));
        }
    }
}

/*
====================================
Create this view: Views/Job/Publish.cshtml
====================================

@model Vocentra.Models.Job
@{
    ViewData["Title"] = "Publish Job";
    var days = (int)(ViewBag.Days ?? 0);
    var amount = (decimal)(ViewBag.Amount ?? 0m);
}

<h2>Publish: @Model.Title</h2>

<p><b>Deadline:</b> @Model.ApplicationDeadline?.ToString("yyyy-MM-dd")</p>
<p><b>Billing:</b> R7/day</p>
<p><b>Days:</b> @days</p>
<p><b>Total:</b> R@amount.ToString("0.00")</p>

@if (Model.IsPaid && string.Equals(Model.Status, "Active", StringComparison.OrdinalIgnoreCase))
{
    <p><b>Status:</b> Active (Paid)</p>
}
else
{
    <form method="post" action="/jobs/@Model.Id/pay">
        <button type="submit">Pay with PayFast to Publish</button>
    </form>
    <p style="margin-top:10px;opacity:.8;">Your job goes live only after PayFast confirms payment (ITN).</p>
}

*/
