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
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Dashboard
        [HttpGet("/Admin")]
        public async Task<IActionResult> Index()
        {
            ViewBag.JobCount = await _context.Jobs.CountAsync();
            ViewBag.ApplicantCount = await _context.Applicants.CountAsync();
            var jobs = await _context.Jobs.OrderByDescending(j => j.PostedAt).ToListAsync();
            return View(jobs);
        }

        // Manage Jobs
        [HttpGet("/Admin/ManageJobs")]
        public async Task<IActionResult> ManageJobs()
        {
            var jobs = await _context.Jobs.OrderByDescending(j => j.PostedAt).ToListAsync();
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
                // Handle new file upload
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

                _context.Update(job);
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
        [HttpGet("/Admin/Applicants")]
        public async Task<IActionResult> Applicants()
        {
            var applicants = await _context.Applicants
                .Include(a => a.Job)
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            return View(applicants);
        }

        // Helper
        private bool JobExists(int id) => _context.Jobs.Any(j => j.Id == id);
    }
}
