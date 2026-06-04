using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Filters;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize]
    public class ResidentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;
        private readonly ResidentImportService _importService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResidentProfileService _residentProfileService;
        private readonly ResidentAccountService _accountService;
        private readonly ResidentLoginProvisioningService _loginProvisioning;
        private readonly ResidentBulkLoginService _bulkLoginService;
        private readonly CommitteeAccessService _committeeAccess;
        private readonly CommitteeLoginProvisioningService _committeeProvisioning;
        private readonly LoginIdentityService _loginIdentity;
        private readonly AuditLogService _auditLog;

        public ResidentsController(
            ApplicationDbContext context,
            MaintenanceBillingService billingService,
            ResidentImportService importService,
            UserManager<ApplicationUser> userManager,
            ResidentProfileService residentProfileService,
            ResidentAccountService accountService,
            ResidentLoginProvisioningService loginProvisioning,
            ResidentBulkLoginService bulkLoginService,
            CommitteeAccessService committeeAccess,
            CommitteeLoginProvisioningService committeeProvisioning,
            LoginIdentityService loginIdentity,
            AuditLogService auditLog)
        {
            _context = context;
            _billingService = billingService;
            _importService = importService;
            _userManager = userManager;
            _residentProfileService = residentProfileService;
            _accountService = accountService;
            _loginProvisioning = loginProvisioning;
            _bulkLoginService = bulkLoginService;
            _committeeAccess = committeeAccess;
            _committeeProvisioning = committeeProvisioning;
            _loginIdentity = loginIdentity;
            _auditLog = auditLog;
        }

        [Authorize(Roles = "Admin,Resident")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CanManageResidents = await _committeeAccess.CanManageResidentsAsync(User);
            ViewBag.CanChangeDesignation =
                await _committeeAccess.CanChangeCommitteeDesignationAsync(User);
            ViewBag.IsSystemAdmin = _committeeAccess.IsSystemAdmin(user);
            ViewBag.ResidentsWithoutLogin = await _bulkLoginService.CountResidentsWithoutLoginAsync();

            var residentsQuery = _context.Residents.AsQueryable();
            if (ViewBag.CanChangeDesignation != true)
            {
                residentsQuery = residentsQuery.Where(r =>
                    r.MemberType != ResidentMemberType.AssociationAdmin);
            }

            var residents = await residentsQuery
                .OrderBy(r => r.FlatNumber)
                .ThenBy(r => r.OwnerName)
                .ToListAsync();
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
                    var loginBroken = !string.IsNullOrEmpty(r.UserId) && !users.ContainsKey(r.UserId);
                    var mustChange = hasLogin && users[r.UserId!].MustChangePassword;
                    return new ResidentIndexItem
                    {
                        Resident = r,
                        HasLogin = hasLogin,
                        LoginBroken = loginBroken,
                        MustChangePassword = mustChange
                    };
                }).ToList()
            };

            return View(model);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> CreateLogin(int? id)
        {
            var resident = await GetResidentAsync(id);
            if (resident == null)
            {
                return NotFound();
            }

            await _loginProvisioning.ClearBrokenLoginLinkAsync(resident);

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                var linkedUser = await _userManager.FindByIdAsync(resident.UserId);
                if (linkedUser != null)
                {
                    TempData["Error"] =
                        "This resident already has a login. Use Reset password on the list to issue a new temporary password.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!LoginIdentityService.IsValidLoginPhone(resident.PhoneNumber))
            {
                TempData["Error"] = "Add a valid mobile number (10+ digits) on the resident record before creating a login.";
                return RedirectToAction(nameof(Edit), new { id = resident.Id });
            }

            ViewBag.DefaultPassword =
                _accountService.GenerateDefaultPassword(resident.FlatNumber);
            ViewBag.RepairingLegacyLogin =
                await _loginIdentity.FindUserForResidentAsync(resident) != null;

            var available = await GetAvailableCommitteeDesignationsAsync();
            return View(new CreateResidentLoginViewModel
            {
                ResidentId = resident.Id,
                ResidentName = resident.OwnerName,
                FlatNumber = resident.FlatNumber,
                PhoneNumber = LoginIdentityService.NormalizeLoginPhone(resident.PhoneNumber),
                NotificationEmail = string.IsNullOrWhiteSpace(resident.Email) ? null : resident.Email.Trim(),
                GrantCommitteeAdminAccess = resident.MemberType == ResidentMemberType.AssociationAdmin,
                AssociationDesignation = resident.AssociationDesignation,
                AvailableDesignations = available
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> CreateLogin(CreateResidentLoginViewModel model)
        {
            var resident = await GetResidentAsync(model.ResidentId);
            if (resident == null)
            {
                return NotFound();
            }

            await _loginProvisioning.ClearBrokenLoginLinkAsync(resident);

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                var linkedUser = await _userManager.FindByIdAsync(resident.UserId);
                if (linkedUser != null && !model.GrantCommitteeAdminAccess)
                {
                    TempData["Error"] =
                        "This resident already has a login. Use Reset password to issue a new temporary password.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!LoginIdentityService.IsValidLoginPhone(model.PhoneNumber))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "A valid mobile number (10+ digits) is required.");
            }

            await ValidateCommitteeLoginChoiceAsync(model);

            if (!ModelState.IsValid)
            {
                await PrepareCreateLoginViewModelAsync(model, resident);
                return View(model);
            }

            ResidentLoginProvisioningResult provisionResult;

            if (model.GrantCommitteeAdminAccess)
            {
                provisionResult = await _committeeProvisioning.PromoteResidentToCommitteeAsync(
                    resident.Id,
                    model.AssociationDesignation!,
                    model.NotificationEmail,
                    model.PhoneNumber);
            }
            else
            {
                if (resident.MemberType == ResidentMemberType.AssociationAdmin)
                {
                    ModelState.AddModelError(
                        nameof(model.GrantCommitteeAdminAccess),
                        "This resident is marked as committee. Enable committee admin access and select a role, or change member type on Edit.");
                    await PrepareCreateLoginViewModelAsync(model, resident);
                    return View(model);
                }

                provisionResult = await _loginProvisioning.CreateLoginForResidentAsync(
                    resident,
                    phoneOverride: model.PhoneNumber,
                    notificationEmailOverride: model.NotificationEmail);
            }

            if (!provisionResult.Succeeded)
            {
                foreach (var error in provisionResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await PrepareCreateLoginViewModelAsync(model, resident);
                return View(model);
            }

            TempData["GeneratedLoginPhone"] = provisionResult.LoginPhone;
            TempData["GeneratedPassword"] = provisionResult.TemporaryPassword;
            TempData["GeneratedNotificationEmail"] = provisionResult.NotificationEmail;
            TempData["CredentialsIsReset"] = false;
            if (model.GrantCommitteeAdminAccess)
            {
                TempData["Success"] =
                    $"Login created with committee admin access ({model.AssociationDesignation}). Share credentials securely.";
                await _auditLog.LogAsync(
                    AuditActions.LoginCreated,
                    $"Committee login created as {model.AssociationDesignation}. Mobile {provisionResult.LoginPhone}.",
                    entityType: "Resident",
                    entityId: resident.Id,
                    flatNumber: resident.FlatNumber);
            }
            else
            {
                await _auditLog.LogAsync(
                    AuditActions.LoginCreated,
                    $"Resident login created. Mobile {provisionResult.LoginPhone}.",
                    entityType: "Resident",
                    entityId: resident.Id,
                    flatNumber: resident.FlatNumber);
            }

            return RedirectToAction(nameof(LoginCredentials), new { id = resident.Id });
        }

        [Authorize(Roles = "Admin")]
        [RequirePresidentOrSecretary]
        public async Task<IActionResult> ChangeDesignation(int? id)
        {
            var resident = await GetResidentAsync(id);
            if (resident == null)
            {
                return NotFound();
            }

            return View(await BuildChangeDesignationViewModelAsync(resident));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequirePresidentOrSecretary]
        public async Task<IActionResult> ChangeDesignation(ChangeCommitteeDesignationViewModel model)
        {
            var resident = await GetResidentAsync(model.ResidentId);
            if (resident == null)
            {
                return NotFound();
            }

            if (model.RemoveCommitteeRole && !string.IsNullOrWhiteSpace(model.NewDesignation))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Choose either a new committee role or remove committee role, not both.");
            }

            if (!model.RemoveCommitteeRole && string.IsNullOrWhiteSpace(model.NewDesignation))
            {
                ModelState.AddModelError(
                    nameof(model.NewDesignation),
                    "Select a committee role or check Remove committee role.");
            }

            if (!ModelState.IsValid)
            {
                var rebuild = await BuildChangeDesignationViewModelAsync(resident);
                rebuild.NewDesignation = model.NewDesignation;
                rebuild.RemoveCommitteeRole = model.RemoveCommitteeRole;
                return View(rebuild);
            }

            var result = await _committeeProvisioning.ChangeResidentDesignationAsync(
                model.ResidentId,
                model.NewDesignation,
                model.RemoveCommitteeRole);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                var rebuild = await BuildChangeDesignationViewModelAsync(resident);
                rebuild.NewDesignation = model.NewDesignation;
                rebuild.RemoveCommitteeRole = model.RemoveCommitteeRole;
                return View(rebuild);
            }

            TempData["Success"] = model.RemoveCommitteeRole
                ? $"{resident.OwnerName} is now a regular owner (login unchanged)."
                : $"{resident.OwnerName} is now {model.NewDesignation} with committee admin access.";

            await _auditLog.LogAsync(
                AuditActions.DesignationChanged,
                model.RemoveCommitteeRole
                    ? "Removed committee role; reverted to Owner."
                    : $"Assigned committee role: {model.NewDesignation}.",
                entityType: "Resident",
                entityId: resident.Id,
                flatNumber: resident.FlatNumber);

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LoginCredentials(int? id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!_committeeAccess.IsSystemAdmin(user) &&
                !await _committeeAccess.IsCommitteeAdminAsync(user))
            {
                return Forbid();
            }

            var loginPhone = TempData["GeneratedLoginPhone"]?.ToString();
            var password = TempData["GeneratedPassword"]?.ToString();

            if (string.IsNullOrEmpty(loginPhone) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Credentials are only shown once. Use Reset password to generate new credentials.";
                return RedirectToAction(nameof(Index));
            }

            var resident = _context.Residents.Find(id);
            if (resident == null)
            {
                return NotFound();
            }

            TempData.Keep("GeneratedLoginPhone");
            TempData.Keep("GeneratedPassword");
            TempData.Keep("GeneratedNotificationEmail");
            TempData.Keep("CredentialsIsReset");

            return View(new LoginCredentialsViewModel
            {
                ResidentId = resident.Id,
                ResidentName = resident.OwnerName,
                FlatNumber = resident.FlatNumber,
                LoginPhone = loginPhone,
                NotificationEmail = TempData["GeneratedNotificationEmail"]?.ToString(),
                TemporaryPassword = password,
                IsPasswordReset = TempData["CredentialsIsReset"] as bool? == true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
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
                await _loginProvisioning.ClearBrokenLoginLinkAsync(resident);
                TempData["Error"] =
                    "Login account was missing and has been cleared. Use Create login to set up mobile login again.";
                return RedirectToAction(nameof(CreateLogin), new { id = resident.Id });
            }

            if (SystemAdminConstants.IsSystemAdmin(user))
            {
                TempData["Error"] = "The system administrator password cannot be reset.";
                return RedirectToAction(nameof(Index));
            }

            var tempPassword = _accountService.GenerateDefaultPassword(resident.FlatNumber);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);

            if (!resetResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", resetResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);

            TempData["GeneratedLoginPhone"] = user.UserName ?? resident.PhoneNumber;
            TempData["GeneratedPassword"] = tempPassword;
            TempData["GeneratedNotificationEmail"] = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
            TempData["CredentialsIsReset"] = true;
            TempData["Success"] =
                "Password reset to the default (apartment prefix + flat). Share it with the resident.";

            return RedirectToAction(nameof(LoginCredentials), new { id = resident.Id });
        }

        [Authorize(Roles = "Admin,Resident")]
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

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Create()
        {
            ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
            return View(new CreateResidentViewModel
            {
                AvailableDesignations = await GetAvailableCommitteeDesignationsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Create(CreateResidentViewModel model)
        {
            var resident = MapToResident(model);
            await ValidateAssociationAdminAsync(resident, excludeId: null);

            if (model.CreateLoginAccount &&
                !LoginIdentityService.IsValidLoginPhone(model.PhoneNumber))
            {
                ModelState.AddModelError(
                    nameof(model.PhoneNumber),
                    "A valid mobile number is required when creating a login account.");
            }

            if (model.CreateLoginAccount)
            {
                await ValidateCommitteeLoginChoiceAsync(model);
            }

            ValidateTenantPropertyOwner(resident);

            if (!ModelState.IsValid)
            {
                ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
                model.AvailableDesignations = await GetAvailableCommitteeDesignationsAsync();
                return View(model);
            }

            resident.FlatNumber = resident.FlatNumber.Trim();
            NormalizePropertyOwnerName(resident);
            _context.Add(resident);
            await _context.SaveChangesAsync();
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            if (model.CreateLoginAccount)
            {
                ResidentLoginProvisioningResult provisionResult;

                if (model.GrantCommitteeAdminAccess)
                {
                    provisionResult = await _committeeProvisioning.PromoteResidentToCommitteeAsync(
                        resident.Id,
                        model.AssociationDesignation!,
                        string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim());
                }
                else
                {
                    provisionResult = await _loginProvisioning.CreateLoginForResidentAsync(resident);
                }

                if (!provisionResult.Succeeded)
                {
                    TempData["Error"] = string.Join(
                        " ",
                        provisionResult.Errors);
                    return RedirectToAction(nameof(Index));
                }

                TempData["GeneratedLoginPhone"] = provisionResult.LoginPhone;
                TempData["GeneratedPassword"] = provisionResult.TemporaryPassword;
                TempData["GeneratedNotificationEmail"] = provisionResult.NotificationEmail;
                TempData["CredentialsIsReset"] = false;
                TempData["Success"] = model.GrantCommitteeAdminAccess
                    ? $"Resident created with committee admin access ({model.AssociationDesignation}). Share credentials on the next screen."
                    : "Resident created. Share the temporary password with them (shown on the next screen).";

                return RedirectToAction(nameof(LoginCredentials), new { id = resident.Id });
            }

            TempData["Success"] = "Resident created. Use Create login on the list to generate a temporary password.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
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

            ViewBag.CanChangeDesignation =
                await _committeeAccess.CanChangeCommitteeDesignationAsync(User);

            return View(resident);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,FlatNumber,OwnerName,PhoneNumber,Email,MemberType,AssociationDesignation,PropertyOwnerName,OwnerContactNumber,CreatedDate,UserId")]
            Resident resident)
        {
            if (id != resident.Id)
            {
                return NotFound();
            }

            var stored = await _context.Residents.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == resident.Id);

            if (stored == null)
            {
                return NotFound();
            }

            var canChangeDesignation =
                await _committeeAccess.CanChangeCommitteeDesignationAsync(User);

            if (!canChangeDesignation)
            {
                resident.MemberType = stored.MemberType;
                resident.AssociationDesignation = stored.AssociationDesignation;
            }

            ResidentMemberHelper.ApplyMemberType(resident, resident.MemberType);

            if (canChangeDesignation)
            {
                await ValidateAssociationAdminAsync(resident, resident.Id);
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

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
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

            await _auditLog.LogAsync(
                AuditActions.ResidentsImported,
                $"Excel import: {result.AddedCount} added, {result.SkippedCount} skipped on sheet {result.SheetName ?? "n/a"}.");

            return View(result);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public IActionResult DownloadTemplate()
        {
            var bytes = _importService.GenerateTemplate();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ResidentsImportTemplate.xlsx");
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> BulkCreateLogins()
        {
            var withoutLogin = await _bulkLoginService.CountResidentsWithoutLoginAsync();
            if (withoutLogin == 0)
            {
                TempData["Success"] = "Every resident already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            return View(new BulkCreateLoginsViewModel
            {
                ResidentsWithoutLogin = withoutLogin,
                ResidentsWithValidPhone = await _bulkLoginService.CountResidentsWithoutLoginAndPhoneAsync(),
                PasswordFormatDescription = _accountService.DescribeDefaultPasswordFormat()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> BulkCreateLogins(BulkCreateLoginsViewModel model)
        {
            model.ResidentsWithoutLogin = await _bulkLoginService.CountResidentsWithoutLoginAsync();
            model.ResidentsWithValidPhone =
                await _bulkLoginService.CountResidentsWithoutLoginAndPhoneAsync();

            if (model.ResidentsWithoutLogin == 0)
            {
                TempData["Success"] = "Every resident already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                model.PasswordFormatDescription = _accountService.DescribeDefaultPasswordFormat();
                return View(model);
            }

            var result = await _bulkLoginService.CreateLoginsAsync();

            if (result.CreatedCount == 0)
            {
                TempData["Error"] =
                    "No logins were created. Check skipped rows below or add emails to resident records.";
            }
            else
            {
                TempData["Success"] =
                    $"Created {result.CreatedCount} login account(s). Download the Excel file and share credentials securely.";
            }

            return View("BulkLoginResult", result);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public IActionResult DownloadBulkLoginCredentials(string? id)
        {
            var bytes = _bulkLoginService.GetCachedSpreadsheet(id);
            if (bytes == null || bytes.Length == 0)
            {
                TempData["Error"] = "Download link expired. Run bulk create logins again to generate a new file.";
                return RedirectToAction(nameof(Index));
            }

            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ResidentLoginCredentials_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
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
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
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

        private static Resident MapToResident(CreateResidentViewModel model)
        {
            var resident = new Resident
            {
                FlatNumber = model.FlatNumber,
                OwnerName = model.OwnerName,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                PropertyOwnerName = model.PropertyOwnerName,
                OwnerContactNumber = model.OwnerContactNumber
            };

            var memberType = model.MemberType == ResidentMemberType.AssociationAdmin
                ? ResidentMemberType.Owner
                : model.MemberType;
            ResidentMemberHelper.ApplyMemberType(resident, memberType);

            return resident;
        }

        private async Task ValidateAssociationAdminAsync(Resident resident, int? excludeId)
        {
            if (resident.MemberType != ResidentMemberType.AssociationAdmin)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(resident.AssociationDesignation) ||
                !AssociationDesignations.All.Contains(
                    resident.AssociationDesignation.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(resident.AssociationDesignation),
                    "Select a valid association designation.");
                return;
            }

            resident.AssociationDesignation =
                AssociationDesignations.All.First(d =>
                    d.Equals(resident.AssociationDesignation.Trim(), StringComparison.OrdinalIgnoreCase));

            var adminQuery = _context.Residents
                .Where(r => r.MemberType == ResidentMemberType.AssociationAdmin);

            if (excludeId.HasValue)
            {
                adminQuery = adminQuery.Where(r => r.Id != excludeId.Value);
            }

            var count = await adminQuery.CountAsync();
            if (count >= AssociationDesignations.MaxAssociationAdmins)
            {
                ModelState.AddModelError(
                    nameof(resident.MemberType),
                    $"The association already has {AssociationDesignations.MaxAssociationAdmins} committee admins.");
                return;
            }

            var designationTaken = await adminQuery.AnyAsync(r =>
                r.AssociationDesignation == resident.AssociationDesignation);

            if (designationTaken)
            {
                ModelState.AddModelError(
                    nameof(resident.AssociationDesignation),
                    $"{resident.AssociationDesignation} is already assigned to another member.");
            }
        }

        private void ValidateTenantPropertyOwner(Resident resident)
        {
            if (resident.MemberType != ResidentMemberType.Tenant)
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

        private async Task<ChangeCommitteeDesignationViewModel> BuildChangeDesignationViewModelAsync(
            Resident resident)
        {
            var committee = await _context.Residents
                .Where(r => r.MemberType == ResidentMemberType.AssociationAdmin)
                .ToListAsync();

            var assignments = AssociationDesignations.All.Select(d =>
            {
                var holder = committee.FirstOrDefault(c =>
                    c.AssociationDesignation != null &&
                    c.AssociationDesignation.Equals(d, StringComparison.OrdinalIgnoreCase));

                return new DesignationAssignmentRow
                {
                    Designation = d,
                    AssignedTo = holder?.OwnerName,
                    IsVacant = holder == null
                };
            }).ToList();

            var available = AssociationDesignations.All
                .Where(d => !committee.Any(c =>
                    c.Id != resident.Id &&
                    c.AssociationDesignation != null &&
                    c.AssociationDesignation.Equals(d, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var isCommittee = resident.MemberType == ResidentMemberType.AssociationAdmin;
            var currentLabel = isCommittee
                ? resident.AssociationDesignation ?? "Committee (no role set)"
                : ResidentMemberHelper.GetMemberTypeLabel(resident);

            return new ChangeCommitteeDesignationViewModel
            {
                ResidentId = resident.Id,
                ResidentName = resident.OwnerName,
                FlatNumber = resident.FlatNumber,
                HasLogin = !string.IsNullOrEmpty(resident.UserId),
                IsCommitteeMember = isCommittee,
                CurrentRoleLabel = currentLabel,
                NewDesignation = isCommittee ? resident.AssociationDesignation : null,
                AvailableDesignations = available,
                CurrentAssignments = assignments
            };
        }

        private async Task<List<string>> GetAvailableCommitteeDesignationsAsync()
        {
            var taken = await _context.Residents
                .Where(r =>
                    r.MemberType == ResidentMemberType.AssociationAdmin &&
                    r.AssociationDesignation != null)
                .Select(r => r.AssociationDesignation!)
                .ToListAsync();

            return AssociationDesignations.All
                .Where(d => !taken.Any(t =>
                    t.Equals(d, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private async Task ValidateCommitteeLoginChoiceAsync(CreateResidentLoginViewModel model)
        {
            await ValidateCommitteeLoginChoiceCoreAsync(
                model.GrantCommitteeAdminAccess,
                model.AssociationDesignation,
                nameof(model.AssociationDesignation));
        }

        private async Task ValidateCommitteeLoginChoiceAsync(CreateResidentViewModel model)
        {
            await ValidateCommitteeLoginChoiceCoreAsync(
                model.GrantCommitteeAdminAccess,
                model.AssociationDesignation,
                nameof(model.AssociationDesignation));
        }

        private async Task ValidateCommitteeLoginChoiceCoreAsync(
            bool grantCommitteeAccess,
            string? designation,
            string fieldName)
        {
            if (!grantCommitteeAccess)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(designation) ||
                !AssociationDesignations.All.Contains(
                    designation.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(fieldName, "Select a committee role (President, Secretary, etc.).");
                return;
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation.Trim(), StringComparison.OrdinalIgnoreCase));

            var taken = await _context.Residents.AnyAsync(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin &&
                r.AssociationDesignation == designation);

            if (taken)
            {
                ModelState.AddModelError(
                    fieldName,
                    $"{designation} is already assigned. Pick another role or use Committee accounts.");
            }
        }

        private async Task PrepareCreateLoginViewModelAsync(
            CreateResidentLoginViewModel model,
            Resident resident)
        {
            model.ResidentName = resident.OwnerName;
            model.FlatNumber = resident.FlatNumber;
            model.AvailableDesignations = await GetAvailableCommitteeDesignationsAsync();
            ViewBag.DefaultPassword =
                _accountService.GenerateDefaultPassword(resident.FlatNumber);
            ViewBag.RepairingLegacyLogin =
                await _loginIdentity.FindUserForResidentAsync(resident) != null;
        }

        private static void NormalizePropertyOwnerName(Resident resident)
        {
            if (resident.MemberType != ResidentMemberType.Tenant)
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
