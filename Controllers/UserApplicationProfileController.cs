using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;
using System.Threading.Tasks;
using System.Linq;

namespace Vocentra.Controllers
{
    [Authorize]
    public class UserApplicationProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FileStorageService _fileStorage;

        public UserApplicationProfileController(AppDbContext db, UserManager<ApplicationUser> userManager, FileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User);
            var profile = _db.UserApplicationProfiles.FirstOrDefault(p => p.UserId == userId);
            return View(profile ?? new UserApplicationProfile { UserId = userId });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UserApplicationProfile model, IFormFile cvFile, IFormFile coverFile)
        {
            var userId = _userManager.GetUserId(User);
            var profile = _db.UserApplicationProfiles.FirstOrDefault(p => p.UserId == userId);

            if (profile == null)
            {
                profile = new UserApplicationProfile { UserId = userId };
                _db.UserApplicationProfiles.Add(profile);
            }

            profile.FullName = model.FullName;
            profile.Email = model.Email;
            profile.Phone = model.Phone;
            profile.ExperienceJson = model.ExperienceJson;
            profile.EducationJson = model.EducationJson;
            profile.SkillsJson = model.SkillsJson;
            profile.OtherFieldsJson = model.OtherFieldsJson;

            if (cvFile != null)
                profile.ProfileCvPath = await _fileStorage.SaveFileAsync(cvFile, "cv_uploads");

            if (coverFile != null)
                profile.CoverLetterPath = await _fileStorage.SaveFileAsync(coverFile, "coverletter_uploads");

            profile.UpdatedAt = System.DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Profile saved successfully.";
            return RedirectToAction("Edit");
        }
    }
}
