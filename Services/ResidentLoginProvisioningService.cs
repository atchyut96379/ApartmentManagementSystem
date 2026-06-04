using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class ResidentLoginProvisioningResult
    {
        public bool Succeeded { get; set; }

        /// <summary>Login username (normalized mobile number).</summary>
        public string? LoginPhone { get; set; }

        /// <summary>Optional real email for notifications (not used to sign in).</summary>
        public string? NotificationEmail { get; set; }

        public string? TemporaryPassword { get; set; }

        public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();
    }

    public class ResidentLoginProvisioningService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResidentProfileService _residentProfileService;
        private readonly MaintenanceBillingService _billingService;
        private readonly ResidentAccountService _accountService;
        private readonly LoginIdentityService _loginIdentity;
        private readonly SocietySettings _societySettings;

        public ResidentLoginProvisioningService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ResidentProfileService residentProfileService,
            MaintenanceBillingService billingService,
            ResidentAccountService accountService,
            LoginIdentityService loginIdentity,
            IOptions<SocietySettings> societySettings)
        {
            _context = context;
            _userManager = userManager;
            _residentProfileService = residentProfileService;
            _billingService = billingService;
            _accountService = accountService;
            _loginIdentity = loginIdentity;
            _societySettings = societySettings.Value;
        }

        /// <summary>Clears broken UserId links so Create login can run again.</summary>
        public async Task<bool> ClearBrokenLoginLinkAsync(Resident resident)
        {
            return await _loginIdentity.ClearOrphanResidentUserIdAsync(resident);
        }

        public async Task<ResidentLoginProvisioningResult> CreateLoginForResidentAsync(
            Resident resident,
            string? phoneOverride = null,
            string? notificationEmailOverride = null)
        {
            await ClearBrokenLoginLinkAsync(resident);

            var loginPhone = LoginIdentityService.NormalizeLoginPhone(
                phoneOverride ?? resident.PhoneNumber);

            if (!LoginIdentityService.IsValidLoginPhone(loginPhone))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[]
                    {
                        "A valid mobile number (at least 10 digits) is required to create a login."
                    }
                };
            }

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                var existingLinked = await _userManager.FindByIdAsync(resident.UserId);
                if (existingLinked != null)
                {
                    return await RepairExistingLoginAsync(
                        resident,
                        existingLinked,
                        loginPhone,
                        notificationEmailOverride);
                }
            }

            var legacyUser = await _loginIdentity.FindUserForResidentAsync(resident);
            if (legacyUser != null && !SystemAdminConstants.IsSystemAdmin(legacyUser))
            {
                return await RepairExistingLoginAsync(
                    resident,
                    legacyUser,
                    loginPhone,
                    notificationEmailOverride);
            }

            var phoneUser = await _loginIdentity.FindUserByLoginIdAsync(loginPhone);
            if (phoneUser != null && !SystemAdminConstants.IsSystemAdmin(phoneUser))
            {
                if (await IsSamePersonAsync(resident, phoneUser))
                {
                    return await RepairExistingLoginAsync(
                        resident,
                        phoneUser,
                        loginPhone,
                        notificationEmailOverride);
                }

                return new ResidentLoginProvisioningResult
                {
                    Errors = new[]
                    {
                        "Another account already uses this mobile number. Check the Residents list or contact the system Admin."
                    }
                };
            }

            var notificationEmail = ResolveNotificationEmail(
                loginPhone,
                notificationEmailOverride,
                resident.Email);

            var tempPassword = _accountService.GenerateDefaultPassword(resident.FlatNumber);
            var user = new ApplicationUser
            {
                UserName = loginPhone,
                Email = notificationEmail,
                FullName = resident.OwnerName.Trim(),
                FlatNumber = resident.FlatNumber.Trim(),
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = createResult.Errors.Select(e => e.Description)
                };
            }

            await EnsureResidentRoleAsync(user, resident);

            resident.UserId = user.Id;
            resident.PhoneNumber = loginPhone;
            if (!string.IsNullOrWhiteSpace(notificationEmailOverride))
            {
                resident.Email = notificationEmailOverride.Trim();
            }

            await _context.SaveChangesAsync();
            await _residentProfileService.LinkResidentForUserAsync(user);
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = loginPhone,
                NotificationEmail = string.IsNullOrWhiteSpace(resident.Email)
                    ? null
                    : resident.Email,
                TemporaryPassword = tempPassword
            };
        }

        private async Task<ResidentLoginProvisioningResult> RepairExistingLoginAsync(
            Resident resident,
            ApplicationUser user,
            string loginPhone,
            string? notificationEmailOverride)
        {
            if (!PhoneNumberHelper.Match(user.UserName, loginPhone))
            {
                var setName = await _userManager.SetUserNameAsync(user, loginPhone);
                if (!setName.Succeeded)
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = setName.Errors.Select(e => e.Description)
                    };
                }
            }

            var notificationEmail = ResolveNotificationEmail(
                loginPhone,
                notificationEmailOverride,
                resident.Email,
                user.Email);

            if (!string.Equals(user.Email, notificationEmail, StringComparison.OrdinalIgnoreCase))
            {
                var setEmail = await _userManager.SetEmailAsync(user, notificationEmail);
                if (!setEmail.Succeeded)
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = setEmail.Errors.Select(e => e.Description)
                    };
                }
            }

            user.FullName = resident.OwnerName.Trim();
            user.FlatNumber = resident.FlatNumber.Trim();
            user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);

            var tempPassword = _accountService.GenerateDefaultPassword(resident.FlatNumber);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
            if (!resetResult.Succeeded)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = resetResult.Errors.Select(e => e.Description)
                };
            }

            await EnsureResidentRoleAsync(user, resident);

            resident.UserId = user.Id;
            resident.PhoneNumber = loginPhone;
            if (!string.IsNullOrWhiteSpace(notificationEmailOverride))
            {
                resident.Email = notificationEmailOverride.Trim();
            }

            await _context.SaveChangesAsync();
            await _residentProfileService.LinkResidentForUserAsync(user);

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = loginPhone,
                NotificationEmail = string.IsNullOrWhiteSpace(resident.Email)
                    ? null
                    : resident.Email,
                TemporaryPassword = tempPassword
            };
        }

        private async Task EnsureResidentRoleAsync(ApplicationUser user, Resident resident)
        {
            if (!await _userManager.IsInRoleAsync(user, "Resident"))
            {
                await _userManager.AddToRoleAsync(user, "Resident");
            }

            if (resident.MemberType == ResidentMemberType.AssociationAdmin &&
                !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.AddToRoleAsync(user, "Admin");
            }
        }

        private async Task<bool> IsSamePersonAsync(Resident resident, ApplicationUser user)
        {
            if (!string.IsNullOrEmpty(resident.UserId) &&
                resident.UserId == user.Id)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(resident.Email))
            {
                var email = resident.Email.Trim();
                if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(user.UserName, email, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (string.Equals(
                    user.FlatNumber?.Trim(),
                    resident.FlatNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    user.FullName?.Trim(),
                    resident.OwnerName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var otherResident = await _context.Residents
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.Id != resident.Id);

            return otherResident == null;
        }

        private string ResolveNotificationEmail(
            string loginPhone,
            string? overrideEmail,
            string? residentEmail,
            string? currentUserEmail = null)
        {
            var notificationEmail = (overrideEmail ?? residentEmail ?? currentUserEmail)?.Trim();
            if (string.IsNullOrWhiteSpace(notificationEmail))
            {
                var domain = string.IsNullOrWhiteSpace(_societySettings.LoginEmailDomain)
                    ? "login.local"
                    : _societySettings.LoginEmailDomain.Trim().ToLowerInvariant();
                notificationEmail = LoginIdentityService.BuildInternalEmail(loginPhone, domain);
            }

            return notificationEmail;
        }
    }
}
