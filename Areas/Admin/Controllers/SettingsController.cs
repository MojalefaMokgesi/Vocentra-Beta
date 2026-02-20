using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vocentra.Services;

namespace Vocentra.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly SettingsService _settings;

        public SettingsController(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task<IActionResult> Index()
        {
            var contact = await _settings.GetAsync("SupportEmail");
            ViewBag.SupportEmail = contact ?? "support@vocentra.example";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string supportEmail)
        {
            if (string.IsNullOrWhiteSpace(supportEmail))
            {
                TempData["Error"] = "Support email is required.";
                return RedirectToAction(nameof(Index));
            }

            await _settings.SetAsync("SupportEmail", supportEmail, "Support contact email");
            TempData["Success"] = "Settings saved.";
            return RedirectToAction(nameof(Index));
        }
    }
}
