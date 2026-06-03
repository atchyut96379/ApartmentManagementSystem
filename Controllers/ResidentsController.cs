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
    [Authorize(Roles = "Admin")]
    public class ResidentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;
        private readonly ResidentImportService _importService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResidentProfileService _residentProfileService;
        private readonly ResidentAccountService _accountService;

        public ResidentsController(
            ApplicationDbContext context,
            MaintenanceBillingService billingService,
            ResidentImportService importService,
            UserManager<ApplicationUser> userManager,
            ResidentProfileService residentProfileService,
            ResidentAccountService accountService)
        {
            _context = context;
            _billingService = billingService;
            _importService = importService;
            _userManager = userManager;
            _residentProfileService = residentProfileService;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var residents = await _context.Residents.OrderBy(r => r.FlatNumber).ToListAsync();
            var userIds = residents
                .Where(r => !string.IsNullOrEmpty(r.UserId))
                .Select(r => r.UserId!)
                .Distinct()
                .ToList();

            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var model = new ResidentsIndexViewModel
            {
                Residents = residents.Select(r =>
                {
                    var hasLogin = !string.IsNullOrEmpty(r.UserId) && users.ContainsKey(r.UserId);
                    var mustChange = hasLogin && users[r.UserId!].MustChangePassword;
                    return new ResidentIndexItem
                    {
                        Resident = r,
                        HasLogin = hasLogin,
                        MustChangePassword = mustChange
                    };
                }).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> CreateLogin(int? id)
        {
            var resident = await GetResidentAsync(id);
            if (resident == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                TempData["Error"] = "This resident already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(resident.Email))
            {
                TempData["Error"] = "Add an email address on the resident record before creating a login.";
                return RedirectToAction(nameof(Edit), new { id = resident.Id });
            }

            return View(new CreateResidentLoginViewModel
            {
                ResidentId = resident.Id,
                ResidentName = resident.OwnerName,
                FlatNumber = resident.FlatNumber,
                Email = resident.Email.Trim()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLogin(CreateResidentLoginViewModel model)
        {
            var resident = await GetResidentAsync(model.ResidentId);
            if (resident == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                TempData["Error"] = "This resident already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            var email = model.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email is required for login.");
            }

            if (!ModelState.IsValid)
            {
                model.ResidentName = resident.OwnerName;
                model.FlatNumber = resident.FlatNumber;
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
                model.ResidentName = resident.OwnerName;
                model.FlatNumber = resident.FlatNumber;
                return View(model);
            }

            var tempPassword = _accountService.GenerateTemporaryPassword();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = resident.OwnerName.Trim(),
                FlatNumber = resident.FlatNumber.Trim(),
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.ResidentName = resident.OwnerName;
                model.FlatNumber = resident.FlatNumber;
                return View(model);
            }

            if (!await _userManager.IsInRoleAsync(user, "Resident"))
            {
                await _userManager.AddToRoleAsync(user, "Resident");
            }

            resident.UserId = user.Id;
            resident.Email = email;
            await _context.SaveChangesAsync();
            await _residentProfileService.LinkResidentForUserAsync(user);
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            TempData["GeneratedEmail"] = email;
            TempData["GeneratedPassword"] = tempPassword;
            TempData["CredentialsIsReset"] = false;

            return RedirectToAction(nameof(LoginCredentials), new { id = resident.Id });
        }

        public IActionResult LoginCredentials(int? id)
        {
            var email = TempData["GeneratedEmail"]?.ToString();
            var password = TempData["GeneratedPassword"]?.ToString();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Credentials are only shown once. Use Reset password to generate new credentials.";
                return RedirectToAction(nameof(Index));
            }

            var resident = _context.Residents.Find(id);
            if (resident == null)
            {
                return NotFound();
            }

            TempData.Keep("GeneratedEmail");
            TempData.Keep("GeneratedPassword");
            TempData.Keep("CredentialsIsReset");

            return View(new LoginCredentialsViewModel
            {
                ResidentId = resident.Id,
                ResidentName = resident.OwnerName,
                FlatNumber = resident.FlatNumber,
                Email = email,
                TemporaryPassword = password,
                IsPasswordReset = TempData["CredentialsIsReset"] as bool? == true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int? id)
        {
            var resident = await GetResidentAsync(id);
            if (resident == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(resident.UserId))
            {
                TempData["Error"] = "This resident does not have a login yet. Use Create login first.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(resident.UserId);
            if (user == null)
            {
                TempData["Error"] = "Login account not found.";
                return RedirectToAction(nameof(Index));
            }

            var tempPassword = _accountService.GenerateTemporaryPassword();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);

            if (!resetResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", resetResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);

            TempData["GeneratedEmail"] = user.Email ?? user.UserName;
            TempData["GeneratedPassword"] = tempPassword;
            TempData["CredentialsIsReset"] = true;
            TempData["Success"] = "Password reset. Share the new temporary password with the resident.";

            return RedirectToAction(nameof(LoginCredentials), new { id = resident.Id });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents
                .FirstOrDefaultAsync(m => m.Id == id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Id,FlatNumber,OwnerName,PhoneNumber,Email,IsOwner,PropertyOwnerName,OwnerContactNumber,CreatedDate")]
            Resident resident)
        {
            ValidateTenantPropertyOwner(resident);

            if (ModelState.IsValid)
            {
                resident.FlatNumber = resident.FlatNumber.Trim();
                NormalizePropertyOwnerName(resident);
                _context.Add(resident);
                await _context.SaveChangesAsync();
                await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(resident);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents.FindAsync(id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,FlatNumber,OwnerName,PhoneNumber,Email,IsOwner,PropertyOwnerName,OwnerContactNumber,CreatedDate,UserId")]
            Resident resident)
        {
            if (id != resident.Id)
            {
                return NotFound();
            }

            ValidateTenantPropertyOwner(resident);

            if (ModelState.IsValid)
            {
                try
                {
                    resident.FlatNumber = resident.FlatNumber.Trim();
                    NormalizePropertyOwnerName(resident);
                    _context.Update(resident);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResidentExists(resident.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(resident);
        }

        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select an Excel file to import.");
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Only .xlsx files are supported.");
                return View();
            }

            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportFromStreamAsync(stream);
            return View(result);
        }

        public IActionResult DownloadTemplate()
        {
            var bytes = _importService.GenerateTemplate();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ResidentsImportTemplate.xlsx");
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents
                .FirstOrDefaultAsync(m => m.Id == id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var resident = await _context.Residents.FindAsync(id);

            if (resident != null)
            {
                _context.Residents.Remove(resident);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<Resident?> GetResidentAsync(int? id)
        {
            if (id == null)
            {
                return null;
            }

            return await _context.Residents.FindAsync(id);
        }

        private bool ResidentExists(int? id)
        {
            return _context.Residents.Any(e => e.Id == id);
        }

        private void ValidateTenantPropertyOwner(Resident resident)
        {
            if (resident.IsOwner)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(resident.PropertyOwnerName) ||
                !string.IsNullOrWhiteSpace(resident.OwnerContactNumber))
            {
                return;
            }

            ModelState.AddModelError(
                nameof(resident.PropertyOwnerName),
                "Tenants require property owner name or owner contact number.");
        }

        private static void NormalizePropertyOwnerName(Resident resident)
        {
            if (resident.IsOwner)
            {
                resident.PropertyOwnerName = null;
                resident.OwnerContactNumber = null;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(resident.PropertyOwnerName))
                {
                    resident.PropertyOwnerName = resident.PropertyOwnerName.Trim();
                }
                else
                {
                    resident.PropertyOwnerName = null;
                }

                if (!string.IsNullOrWhiteSpace(resident.OwnerContactNumber))
                {
                    resident.OwnerContactNumber = resident.OwnerContactNumber.Trim();
                }
                else
                {
                    resident.OwnerContactNumber = null;
                }
            }
        }
    }
}
