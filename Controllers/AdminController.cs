using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using Vocentra.Data;
using Vocentra.Models;

namespace Vocentra.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.AspNetCore.Identity.UserManager<Vocentra.Models.ApplicationUser> _userManager;

        public AdminController(AppDbContext context, IWebHostEnvironment env, Microsoft.AspNetCore.Identity.UserManager<Vocentra.Models.ApplicationUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        // Dashboard
        [HttpGet("/Admin")]
        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                // Non-admin users should manage their own jobs
                return RedirectToAction(nameof(ManageJobs));
            }

            ViewBag.JobCount = await _context.Jobs.CountAsync();
            ViewBag.ApplicantCount = await _context.Applicants.CountAsync();
            var jobs = await _context.Jobs.OrderByDescending(j => j.PostedAt).ToListAsync();
            return View(jobs);
        }

        // Manage Jobs - admins see all jobs, regular authenticated users see only their own jobs
        [HttpGet("/Admin/ManageJobs")]
        public async Task<IActionResult> ManageJobs()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var jobsQuery = _context.Jobs
                .Include(j => j.Applicants)
                .AsQueryable();
            if (!isAdmin)
                jobsQuery = jobsQuery.Where(j => j.OwnerUserId == userId);

            var jobs = await jobsQuery.OrderByDescending(j => j.PostedAt).ToListAsync();
            return View(jobs);
        }

        // Create Job
        [HttpGet("/Admin/Create")]
        public IActionResult Create() => View();

        [HttpPost("/Admin/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Job job)
        {
            if (!ModelState.IsValid) return View(job);

            // Handle file upload
            if (job.CompanyLogoFile != null && job.CompanyLogoFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "company-logos");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileName = Path.GetFileName(job.CompanyLogoFile.FileName);
                var filePath = Path.Combine(uploadDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await job.CompanyLogoFile.CopyToAsync(stream);

                job.CompanyLogoUrl = $"/uploads/company-logos/{fileName}";
            }

            job.PostedAt = DateTime.Now;
            // If admin creates a job, set owner to the admin user id
            job.OwnerUserId = _userManager.GetUserId(User) ?? job.OwnerUserId;

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();
            return RedirectToAction("ManageJobs");
        }

        // Edit Job
        [HttpGet("/Admin/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null) return NotFound();
            return View(job);
        }

        [HttpPost("/Admin/Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Job job)
        {
            if (id != job.Id) return NotFound();
            if (!ModelState.IsValid) return View(job);

            try
            {
                // Load existing entity to avoid overwriting OwnerUserId or other fields
                var existing = await _context.Jobs.FindAsync(id);
                if (existing == null) return NotFound();

                // Handle new file upload
                if (job.CompanyLogoFile != null && job.CompanyLogoFile.Length > 0)
                {
                    var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "company-logos");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    var fileName = Path.GetFileName(job.CompanyLogoFile.FileName);
                    var filePath = Path.Combine(uploadDir, fileName);

                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await job.CompanyLogoFile.CopyToAsync(stream);

                    existing.CompanyLogoUrl = $"/uploads/company-logos/{fileName}";
                }

                // Map editable fields (preserve OwnerUserId)
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

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction("ManageJobs");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(job.Id)) return NotFound();
                throw;
            }
        }

        // Delete Job
        [HttpGet("/Admin/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();
            return View(job);
        }

        [HttpPost("/Admin/Delete/{id}"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null) return NotFound();
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
            return RedirectToAction("ManageJobs");
        }

        // Applicants
        [HttpGet("/Admin/Applicants/{jobId?}")]
        public async Task<IActionResult> Applicants(int? jobId)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var applicantsQuery = _context.Applicants
                .Include(a => a.Job)
                .AsQueryable();

            if (jobId.HasValue)
            {
                applicantsQuery = applicantsQuery.Where(a => a.JobId == jobId.Value);
            }

            if (!isAdmin)
            {
                // Non-admins can only see applicants for their own jobs
                applicantsQuery = applicantsQuery.Where(a => a.Job != null && a.Job.OwnerUserId == userId);
            }

            var applicants = await applicantsQuery.OrderByDescending(a => a.Id).ToListAsync();

            // If a specific job was requested, provide its title to the view
            if (jobId.HasValue)
            {
                var job = await _context.Jobs.FindAsync(jobId.Value);
                ViewBag.JobTitle = job?.Title ?? "Applicants";
                ViewBag.JobId = jobId.Value;
            }
            else
            {
                ViewBag.JobTitle = "All Applicants";
            }

            return View(applicants);
        }

        // Helper
        private bool JobExists(int id) => _context.Jobs.Any(j => j.Id == id);
    }
}
