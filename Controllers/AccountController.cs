using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.ViewModels;

namespace Vocentra.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _context;
        private readonly Services.SettingsService _settingsService;
        private readonly UserManager<ApplicationUser> _userManagerLocal;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, AppDbContext context, Services.SettingsService settingsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _settingsService = settingsService;
            _userManagerLocal = userManager;
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestEmailChange(AccountSettingsViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.NewEmail) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(vm.NewEmail))
            {
                ModelState.AddModelError("NewEmail", "Enter a valid email address.");
                return await Settings();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            // ensure new email is not used by another account
            var existing = await _userManager.FindByEmailAsync(vm.NewEmail);
            if (existing != null && existing.Id != user.Id)
            {
                ModelState.AddModelError("NewEmail", "Email is already in use.");
                return await Settings();
            }

            // generate change email token and save pending email on user
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, vm.NewEmail);

            // store pending email and timestamp
            user.PendingEmail = vm.NewEmail;
            user.PendingEmailRequestedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // build callback link
            var callback = Url.Action(nameof(ConfirmEmailChange), "Account", new { userId = user.Id, email = vm.NewEmail, token }, protocol: Request.Scheme);

            // send email - prefer IEmailSender if available via DI, fallback to log
            try
            {
                var sender = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Identity.UI.Services.IEmailSender)) as Microsoft.AspNetCore.Identity.UI.Services.IEmailSender;
                if (sender != null)
                {
                    await sender.SendEmailAsync(vm.NewEmail, "Confirm your email change", $"Please confirm your email change by clicking <a href=\"{callback}\">here</a>.");
                }
                else
                {
                    // fallback - in development simply log to console
                    System.Console.WriteLine($"Email change link for {vm.NewEmail}: {callback}");
                }
            }
            catch
            {
                // swallow but show message
                TempData["Error"] = "Failed to send confirmation email. Try again later.";
                return RedirectToAction(nameof(Settings));
            }

            TempData["Success"] = "A confirmation email has been sent to the new address. Check your inbox to confirm the change.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string email, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid confirmation link.";
                return RedirectToAction(nameof(Login), "Account");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Login), "Account");
            }

            // ensure the pending email matches
            if (string.IsNullOrWhiteSpace(user.PendingEmail) || !string.Equals(user.PendingEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No pending email change found for this account.";
                return RedirectToAction(nameof(Login), "Account");
            }

            var result = await _userManager.ChangeEmailAsync(user, email, token);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Failed to change email. The link may be invalid or expired.";
                return RedirectToAction(nameof(Login), "Account");
            }

            // if username equals previous email, update username too
            if (user.UserName == user.Email)
            {
                await _userManager.SetUserNameAsync(user, email);
            }

            // clear pending
            user.PendingEmail = null;
            user.PendingEmailRequestedAt = null;
            await _userManager.UpdateAsync(user);

            // refresh sign-in
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Your email address has been changed and verified.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost("/Account/ResendEmailVerification")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailVerification()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action("ConfirmEmail", "Identity/Account", new { userId = user.Id, code }, protocol: Request.Scheme);

            // Here you would send the email using your email sender. For now we set TempData.
            TempData["Success"] = "Verification email sent (simulated).";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost("/Account/Deactivate")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            user.IsDeactivated = true;
            await _userManager.UpdateAsync(user);
            await _signInManager.SignOutAsync();

            TempData["Success"] = "Your account has been deactivated. You can reactivate by logging in again.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                CompanyName = model.CompanyName,
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Apply", "Job");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked out. Try again later.");
                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var applicant = await _context.Applicants.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (applicant == null || !applicant.IsApplicationComplete)
                return RedirectToAction("Apply", "Job");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("/Account/Settings")]
        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            // load user settings
            var userSettings = await _context.UserSettings.FirstOrDefaultAsync(x => x.UserId == user.Id);

            var vm = new AccountSettingsViewModel
            {
                Email = user.Email ?? string.Empty,
                NewEmail = user.PendingEmail,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                CompanyName = user.CompanyName,
                PhoneNumber = user.PhoneNumber,
                EmailNotifications = userSettings?.EmailNotifications ?? true,
                JobAlerts = userSettings?.JobAlerts ?? true,
                EmailVerified = await _userManager.IsEmailConfirmedAsync(user)
            };

            // calculate profile completion
            var total = 4; // first, last, company, phone
            var filled = 0;
            if (!string.IsNullOrWhiteSpace(vm.FirstName)) filled++;
            if (!string.IsNullOrWhiteSpace(vm.LastName)) filled++;
            if (!string.IsNullOrWhiteSpace(vm.CompanyName)) filled++;
            if (!string.IsNullOrWhiteSpace(vm.PhoneNumber)) filled++;
            vm.ProfileCompletion = (int)Math.Round((double)filled / total * 100);

            return View(vm);
        }

        [HttpPost("/Account/Settings")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AccountSettingsViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            // If posted model invalid, rebuild viewmodel from persisted user + posted prefs
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors and try again.";

                var userSettings = await _context.UserSettings.FirstOrDefaultAsync(x => x.UserId == user.Id);
                var rebuilt = new AccountSettingsViewModel
                {
                    Email = user.Email ?? string.Empty,
                    NewEmail = vm.NewEmail,
                    FirstName = vm.FirstName ?? user.FirstName ?? string.Empty,
                    LastName = vm.LastName ?? user.LastName ?? string.Empty,
                    CompanyName = vm.CompanyName ?? user.CompanyName,
                    PhoneNumber = vm.PhoneNumber ?? user.PhoneNumber,
                    EmailNotifications = vm.EmailNotifications || (userSettings?.EmailNotifications ?? true),
                    JobAlerts = vm.JobAlerts || (userSettings?.JobAlerts ?? true),
                    EmailVerified = await _userManager.IsEmailConfirmedAsync(user),
                    EmailChangeRequested = !string.IsNullOrWhiteSpace(user.PendingEmail)
                };

                // compute profile completion based on merged values
                var total = 4;
                var filled = 0;
                if (!string.IsNullOrWhiteSpace(rebuilt.FirstName)) filled++;
                if (!string.IsNullOrWhiteSpace(rebuilt.LastName)) filled++;
                if (!string.IsNullOrWhiteSpace(rebuilt.CompanyName)) filled++;
                if (!string.IsNullOrWhiteSpace(rebuilt.PhoneNumber)) filled++;
                rebuilt.ProfileCompletion = (int)Math.Round((double)filled / total * 100);

                return View(rebuilt);
            }

            // Valid model - persist changes
            user.FirstName = vm.FirstName;
            user.LastName = vm.LastName;
            user.CompanyName = vm.CompanyName;
            user.PhoneNumber = vm.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                TempData["Error"] = "Failed to update account settings.";
                return RedirectToAction(nameof(Settings));
            }

            // Ensure user profile exists and update it
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    UserId = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Phone = user.PhoneNumber
                };
                _context.UserProfiles.Add(profile);
            }
            else
            {
                profile.FullName = $"{user.FirstName} {user.LastName}".Trim();
                profile.Phone = user.PhoneNumber;
                _context.UserProfiles.Update(profile);
            }

            // Save notification preferences
            await _settingsService.SetUserSettingAsync(user.Id, "emailnotifications", vm.EmailNotifications.ToString());
            await _settingsService.SetUserSettingAsync(user.Id, "jobalerts", vm.JobAlerts.ToString());

            await _context.SaveChangesAsync();

            TempData["Success"] = "Account settings updated.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpGet("/Account/SecuritySettings")]
        [HttpGet("/Account/Security")]
        [Authorize]
        public async Task<IActionResult> SecuritySettings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            ViewBag.LastPasswordChange = user.LastPasswordChangedAt?.ToString("f");

            var recent = await _context.Set<Models.SecurityActivityLog>()
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.SecurityActivity = recent.Select(r => new { r.Action, CreatedAt = r.CreatedAt.ToString("g") });

            return View("Security");
        }

        [HttpPost("/Account/ChangePassword")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string CurrentPassword, string NewPassword)
        {
            var confirm = Request.Form["ConfirmPassword"].ToString();
            if (NewPassword != confirm)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(SecuritySettings));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(SecuritySettings));
            }
            user.LastPasswordChangedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // log activity
            _context.Add(new Models.SecurityActivityLog { UserId = user.Id, Action = "Password changed", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(SecuritySettings));
        }

        [HttpPost("/Account/LogoutAllSessions")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutAllSessions()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            // updating security stamp will invalidate other sessions
            await _userManager.UpdateSecurityStampAsync(user);

            _context.Add(new Models.SecurityActivityLog { UserId = user.Id, Action = "Logged out all sessions", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            TempData["Success"] = "All other sessions have been logged out.";
            return RedirectToAction(nameof(SecuritySettings));
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
