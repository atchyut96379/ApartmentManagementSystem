using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ResidentProfileService residentProfileService,
            MaintenanceBillingService billingService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _residentProfileService = residentProfileService;
            _billingService = billingService;
        }

        // =========================================
        // REGISTER PAGE (GET)
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Register()
        {
            return View(new RegisterViewModel { Role = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            var email = model.Email?.Trim() ?? string.Empty;
            model.FlatNumber = model.FlatNumber?.Trim();

            try
            {
                if (string.IsNullOrWhiteSpace(model.FullName))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.FullName), "Full Name is required");
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email is required");
                }

                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Password), "Password is required");
                }

                model.Role = "Admin";

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var existingUser = await _userManager.FindByEmailAsync(email);

                if (existingUser != null)
                {
                    ModelState.AddModelError(
                        "",
                        "An account with this email already exists. Please log in with that email and password.");
                    model.ShowLoginLink = true;
                    model.Password = string.Empty;
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    FullName = model.FullName.Trim(),
                    UserName = email,
                    Email = email,
                    FlatNumber = null,
                    MustChangePassword = false
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }

                    model.Password = string.Empty;
                    return View(model);
                }

                if (!await _roleManager.RoleExistsAsync("Admin"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Admin"));
                }

                if (!await _roleManager.RoleExistsAsync("Resident"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Resident"));
                }

                await _userManager.AddToRoleAsync(user, "Admin");

                TempData["Success"] = "Admin account created. Please login.";
                return RedirectToAction("Login", new { email });
            }
            catch (Exception ex)
            {
                if (await _userManager.FindByEmailAsync(email) is ApplicationUser createdUser)
                {
                    await _userManager.DeleteAsync(createdUser);
                }

                ModelState.AddModelError("", ex.Message);
                model.Password = string.Empty;
            }

            return View(model);
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
                Email = user.Email ?? user.UserName ?? string.Empty
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
                model.Email = user.Email ?? user.UserName ?? string.Empty;
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
        public IActionResult Login(string? email)
        {
            ViewBag.PrefillEmail = email;
            return View();
        }

        // =========================================
        // LOGIN PAGE (POST)
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    ModelState.AddModelError(
                        "",
                        "Email is required");
                }

                if (string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError(
                        "",
                        "Password is required");
                }

                if (!ModelState.IsValid)
                {
                    return View();
                }

                var result =
                    await _signInManager.PasswordSignInAsync(
                        email,
                        password,
                        false,
                        false);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(email);

                    if (user != null)
                    {
                        if (await _userManager.IsInRoleAsync(user, "Resident") ||
                            await _userManager.IsInRoleAsync(user, "Admin"))
                        {
                            await _residentProfileService.EnsureResidentProfileAsync(user);
                        }

                        if (user.MustChangePassword)
                        {
                            return RedirectToAction("ChangePasswordRequired");
                        }

                        if (await _userManager.IsInRoleAsync(user, "Resident"))
                        {
                            user = await _userManager.FindByIdAsync(user.Id);

                            if (user != null && string.IsNullOrWhiteSpace(user.FlatNumber))
                            {
                                return RedirectToAction("CompleteProfile");
                            }
                        }
                    }

                    return RedirectToAction("Index", "Home");
                }

                ViewBag.Error =
                    "Invalid Email or Password";
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
        public IActionResult ChangePasswordRequired()
        {
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

            TempData["Success"] = "Your password has been updated.";
            return RedirectToAction("Index", "Home");
        }
    }
}