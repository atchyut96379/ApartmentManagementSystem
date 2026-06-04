using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ResidentProfileService _residentProfileService;
        private readonly MaintenanceBillingService _billingService;
        private readonly ResidentPasswordResetService _passwordResetService;
        private readonly ResidentAccountService _accountService;
        private readonly LoginIdentityService _loginIdentity;
        private readonly PasswordResetOtpService _otpService;
        private readonly SocietySettings _societySettings;
        private readonly AuditLogService _auditLog;
        private readonly IntegrationsSettingsStore _settingsStore;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ResidentProfileService residentProfileService,
            MaintenanceBillingService billingService,
            ResidentPasswordResetService passwordResetService,
            ResidentAccountService accountService,
            LoginIdentityService loginIdentity,
            PasswordResetOtpService otpService,
            IOptions<SocietySettings> societySettings,
            AuditLogService auditLog,
            IntegrationsSettingsStore settingsStore)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _residentProfileService = residentProfileService;
            _billingService = billingService;
            _passwordResetService = passwordResetService;
            _accountService = accountService;
            _loginIdentity = loginIdentity;
            _otpService = otpService;
            _societySettings = societySettings.Value;
            _auditLog = auditLog;
            _settingsStore = settingsStore;
        }

        // =========================================
        // REGISTER PAGE (GET)
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register()
        {
            var user = await _userManager.GetUserAsync(User);
            if (!SystemAdminConstants.IsSystemAdmin(user))
            {
                return RedirectToAction("Index", "Home");
            }

            TempData["Success"] =
                "Use Committee accounts to create President, Vice President, Secretary, Joint Secretary, and Treasurer.";
            return RedirectToAction("Index", "Committee");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Register(RegisterViewModel model)
        {
            return RedirectToAction("Index", "Committee");
        }

        // =========================================
        // LOGIN PAGE (GET)
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Resident,Admin")]
        public async Task<IActionResult> CompleteProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (user.MustChangePassword)
            {
                return RedirectToAction("ChangePasswordRequired");
            }

            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            if (!string.IsNullOrEmpty(flat))
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new CompleteProfileViewModel
            {
                LoginId = user.UserName ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident,Admin")]
        public async Task<IActionResult> CompleteProfile(CompleteProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (user.MustChangePassword)
            {
                return RedirectToAction("ChangePasswordRequired");
            }

            if (string.IsNullOrWhiteSpace(model.FlatNumber))
            {
                ModelState.AddModelError(nameof(model.FlatNumber), "Flat number is required");
                model.LoginId = user.UserName ?? string.Empty;
                return View(model);
            }

            await _residentProfileService.CreateResidentForUserAsync(
                user,
                model.FlatNumber.Trim(),
                user.Email);

            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                var resident = await _residentProfileService.GetCurrentResidentAsync(User);
                if (resident != null)
                {
                    resident.PhoneNumber = model.PhoneNumber.Trim();
                    await _context.SaveChangesAsync();
                }
            }

            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            TempData["Success"] = "Your profile is set up. You can now view and pay maintenance.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? mobile)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return await RedirectAuthenticatedUserAsync();
            }

            ViewBag.PrefillMobile = mobile;
            var appSettings = _settingsStore.GetApplicationSettings();
            var publicUrl = appSettings.GetAppUrl(Request);
            ViewBag.PublicPortalUrl = publicUrl;
            ViewBag.HasCustomPortalUrl =
                !string.IsNullOrWhiteSpace(appSettings.AppUrl) &&
                !appSettings.AppUrl.Contains("azurewebsites.net", StringComparison.OrdinalIgnoreCase) &&
                !appSettings.AppUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
            return View();
        }

        // =========================================
        // LOGIN PAGE (POST)
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string mobile,
            string password)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
            }

            try
            {
                if (string.IsNullOrWhiteSpace(mobile))
                {
                    ModelState.AddModelError(string.Empty, "Mobile number or Admin username is required.");
                }

                if (string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError(string.Empty, "Password is required.");
                }

                if (!ModelState.IsValid)
                {
                    return View();
                }

                mobile = mobile.Trim();
                ViewBag.PrefillMobile = mobile;

                var existingUser = await _loginIdentity.FindUserByLoginIdAsync(mobile);

                if (existingUser == null &&
                    LoginIdentityService.IsValidLoginPhone(mobile))
                {
                    var loginPhone = LoginIdentityService.NormalizeLoginPhone(mobile);
                    var residents = await _context.Residents.ToListAsync();
                    var matchingResident = residents.FirstOrDefault(r =>
                        PhoneNumberHelper.Match(r.PhoneNumber, loginPhone));

                    if (matchingResident != null)
                    {
                        await _loginIdentity.ClearOrphanResidentUserIdAsync(matchingResident);

                        var expectedPassword = _accountService.GenerateDefaultPassword(
                            matchingResident.FlatNumber);

                        ViewBag.Error =
                            "Your resident record is in the system but login is not set up yet. " +
                            "Ask the committee admin to open Residents, find your flat, and click **Create login**. " +
                            $"Then sign in with this mobile number and temporary password {expectedPassword}, and choose a new password on first login.";
                        return View();
                    }
                }

                if (existingUser == null)
                {
                    ViewBag.Error = "Invalid mobile number or password.";
                    return View();
                }

                var result = await _signInManager.PasswordSignInAsync(
                    existingUser.UserName!,
                    password,
                    false,
                    false);

                if (result.Succeeded)
                {
                    if (LoginIdentityService.IsValidLoginPhone(mobile))
                    {
                        var loginPhone = LoginIdentityService.NormalizeLoginPhone(mobile);
                        var residents = await _context.Residents.ToListAsync();
                        var matchingResident = residents.FirstOrDefault(r =>
                            PhoneNumberHelper.Match(r.PhoneNumber, loginPhone));
                        if (matchingResident != null &&
                            matchingResident.UserId != existingUser.Id)
                        {
                            matchingResident.UserId = existingUser.Id;
                            await _context.SaveChangesAsync();
                        }
                    }

                    if (await _userManager.IsInRoleAsync(existingUser, "Resident") ||
                        await _userManager.IsInRoleAsync(existingUser, "Admin"))
                    {
                        await _residentProfileService.EnsureResidentProfileAsync(existingUser);
                    }

                    if (existingUser.MustChangePassword)
                    {
                        return RedirectToAction("ChangePasswordRequired");
                    }

                    if (await _userManager.IsInRoleAsync(existingUser, "Resident") &&
                        string.IsNullOrWhiteSpace(existingUser.FlatNumber))
                    {
                        return RedirectToAction("CompleteProfile");
                    }

                    return RedirectToAction("Index", "Home");
                }

                var linkedResident = await _context.Residents
                    .FirstOrDefaultAsync(r => r.UserId == existingUser.Id);
                var flatHint = linkedResident?.FlatNumber
                    ?? existingUser.FlatNumber
                    ?? string.Empty;
                if (!string.IsNullOrEmpty(flatHint))
                {
                    var expected = _accountService.GenerateDefaultPassword(flatHint);
                    ViewBag.Error =
                        $"Invalid password. If the admin just created your account, try temporary password {expected}, then set your own password on first login.";
                    return View();
                }

                ViewBag.Error = "Invalid mobile number or password.";
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }

            return View();
        }

        // =========================================
        // LOGOUT
        // =========================================

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(
                "Login",
                "Account");
        }

        // =========================================
        // ACCESS DENIED
        // =========================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePasswordRequired()
        {
            var user = await _userManager.GetUserAsync(User);
            if (SystemAdminConstants.IsSystemAdmin(user))
            {
                TempData["Error"] = "The system administrator password cannot be changed from this application.";
                return RedirectToAction("Index", "Home");
            }

            return View(new ChangePasswordRequiredViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePasswordRequired(ChangePasswordRequiredViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (SystemAdminConstants.IsSystemAdmin(user))
            {
                TempData["Error"] = "The system administrator password cannot be changed.";
                return RedirectToAction("Index", "Home");
            }

            var changeResult = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (!changeResult.Succeeded)
            {
                foreach (var error in changeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Your password has been updated. Use your new password from your next login.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return await RedirectAuthenticatedUserAsync();
            }

            ViewBag.SmsOtpEnabled = _otpService.IsSmsOtpAvailable();
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.Equals(
                    model.MobileNumber.Trim(),
                    SystemAdminConstants.UserName,
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The system administrator account cannot be reset here.");
                return View(model);
            }

            var user = await _passwordResetService.VerifyResidentCredentialsAsync(
                model.MobileNumber,
                model.FlatNumber);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "We could not verify your details. Check your mobile number and flat number, or contact the admin.");
                return View(model);
            }

            TempData["PasswordResetUserId"] = user.Id;

            if (_otpService.IsSmsOtpAvailable())
            {
                var phone = user.UserName ?? model.MobileNumber;
                var (sent, err) = await _otpService.SendOtpAsync(
                    user.Id,
                    phone,
                    _societySettings.ApartmentName ?? "Apartment");

                if (!sent)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        err ?? "Could not send SMS code. Try again or contact the admin.");
                    ViewBag.SmsOtpEnabled = true;
                    return View(model);
                }

                TempData["PasswordResetMaskedPhone"] = MaskPhoneForDisplay(phone);
                return RedirectToAction(nameof(VerifyForgotPasswordOtp));
            }

            return RedirectToAction(nameof(ResetForgottenPassword));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyForgotPasswordOtp()
        {
            if (TempData["PasswordResetUserId"] == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            TempData.Keep("PasswordResetUserId");
            return View(new VerifyForgotPasswordOtpViewModel
            {
                MaskedPhone = TempData["PasswordResetMaskedPhone"]?.ToString() ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult VerifyForgotPasswordOtp(VerifyForgotPasswordOtpViewModel model)
        {
            var userId = TempData["PasswordResetUserId"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (!ModelState.IsValid)
            {
                TempData.Keep("PasswordResetUserId");
                TempData.Keep("PasswordResetMaskedPhone");
                model.MaskedPhone = TempData["PasswordResetMaskedPhone"]?.ToString() ?? "";
                return View(model);
            }

            if (!_otpService.VerifyOtp(userId, model.OtpCode))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Invalid or expired code. Request a new reset.");
                TempData.Keep("PasswordResetUserId");
                TempData.Keep("PasswordResetMaskedPhone");
                model.MaskedPhone = TempData["PasswordResetMaskedPhone"]?.ToString() ?? "";
                return View(model);
            }

            _otpService.ClearOtp(userId);
            TempData.Keep("PasswordResetUserId");
            return RedirectToAction(nameof(ResetForgottenPassword));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetForgottenPassword()
        {
            if (TempData["PasswordResetUserId"] == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            TempData.Keep("PasswordResetUserId");
            return View(new ResetForgottenPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ResetForgottenPassword(ResetForgottenPasswordViewModel model)
        {
            var userId = TempData["PasswordResetUserId"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (!ModelState.IsValid)
            {
                TempData.Keep("PasswordResetUserId");
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                TempData.Keep("PasswordResetUserId");
                return View(model);
            }

            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            await _auditLog.LogAsync(
                AuditActions.PasswordReset,
                "Password reset via forgot-password flow.",
                entityType: "User",
                flatNumber: user.FlatNumber);

            TempData["Success"] =
                "Your password has been reset. Sign in with your mobile number and new password.";
            return RedirectToAction(nameof(Login));
        }

        private static string MaskPhoneForDisplay(string phone)
        {
            var digits = LoginIdentityService.NormalizeLoginPhone(phone);
            if (digits.Length < 4)
            {
                return "****";
            }

            return "******" + digits[^4..];
        }

        private async Task<IActionResult> RedirectAuthenticatedUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction(nameof(Login));
            }

            if (user.MustChangePassword && !SystemAdminConstants.IsSystemAdmin(user))
            {
                return RedirectToAction(nameof(ChangePasswordRequired));
            }

            if (await _userManager.IsInRoleAsync(user, "Resident"))
            {
                var flat = await _residentProfileService.GetFlatForUserAsync(User);
                if (string.IsNullOrWhiteSpace(flat))
                {
                    return RedirectToAction(nameof(CompleteProfile));
                }
            }

            return RedirectToAction("Index", "Home");
        }
    }
}