using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class CommitteeLoginProvisioningService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResidentAccountService _accountService;
        private readonly ResidentLoginProvisioningService _residentLoginProvisioning;
        private readonly LoginIdentityService _loginIdentity;
        private readonly SocietySettings _societySettings;

        public CommitteeLoginProvisioningService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ResidentAccountService accountService,
            ResidentLoginProvisioningService residentLoginProvisioning,
            LoginIdentityService loginIdentity,
            IOptions<SocietySettings> societySettings)
        {
            _context = context;
            _userManager = userManager;
            _accountService = accountService;
            _residentLoginProvisioning = residentLoginProvisioning;
            _loginIdentity = loginIdentity;
            _societySettings = societySettings.Value;
        }

        public async Task<ResidentLoginProvisioningResult> CreateCommitteeAccountAsync(
            string designation,
            string ownerName,
            string flatNumber,
            string phoneNumber,
            string email)
        {
            if (!AssociationDesignations.All.Contains(
                    designation,
                    StringComparer.OrdinalIgnoreCase))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Invalid committee designation." }
                };
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation, StringComparison.OrdinalIgnoreCase));

            var existingDesignation = await _context.Residents.AnyAsync(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin &&
                r.AssociationDesignation == designation);

            if (existingDesignation)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { $"{designation} is already assigned." }
                };
            }

            var committeeCount = await _context.Residents.CountAsync(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin);

            if (committeeCount >= AssociationDesignations.MaxAssociationAdmins)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "All five committee positions are already filled." }
                };
            }

            var loginPhone = LoginIdentityService.NormalizeLoginPhone(phoneNumber);
            if (!LoginIdentityService.IsValidLoginPhone(loginPhone))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "A valid mobile number (10+ digits) is required." }
                };
            }

            if (await _loginIdentity.IsLoginPhoneTakenAsync(loginPhone))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "An account with this mobile number already exists." }
                };
            }

            var flat = flatNumber.Trim();
            var password = _accountService.GenerateDefaultPassword(flat);
            var notificationEmail = string.IsNullOrWhiteSpace(email)
                ? LoginIdentityService.BuildInternalEmail(
                    loginPhone,
                    string.IsNullOrWhiteSpace(_societySettings.LoginEmailDomain)
                        ? "login.local"
                        : _societySettings.LoginEmailDomain)
                : email.Trim();

            var user = new ApplicationUser
            {
                UserName = loginPhone,
                Email = notificationEmail,
                FullName = ownerName.Trim(),
                FlatNumber = flat,
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = createResult.Errors.Select(e => e.Description)
                };
            }

            await _userManager.AddToRoleAsync(user, "Admin");

            var resident = new Resident
            {
                FlatNumber = flat,
                OwnerName = ownerName.Trim(),
                PhoneNumber = loginPhone,
                Email = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim(),
                MemberType = ResidentMemberType.AssociationAdmin,
                AssociationDesignation = designation,
                IsOwner = true,
                UserId = user.Id,
                CreatedDate = DateTime.Now
            };

            _context.Residents.Add(resident);
            await _context.SaveChangesAsync();

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = loginPhone,
                NotificationEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                TemporaryPassword = password
            };
        }

        /// <summary>
        /// Promotes an existing imported resident to a committee role with Admin access (no duplicate row).
        /// </summary>
        public async Task<ResidentLoginProvisioningResult> PromoteResidentToCommitteeAsync(
            int residentId,
            string designation,
            string? notificationEmailOverride = null,
            string? phoneOverride = null)
        {
            var validation = await ValidateDesignationAvailableAsync(designation);
            if (validation != null)
            {
                return validation;
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation, StringComparison.OrdinalIgnoreCase));

            var resident = await _context.Residents.FindAsync(residentId);
            if (resident == null)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Resident not found." }
                };
            }

            var alreadyCommittee = resident.MemberType == ResidentMemberType.AssociationAdmin;

            if (alreadyCommittee && !string.IsNullOrEmpty(resident.UserId))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "This person is already a committee member with a login." }
                };
            }

            if (!alreadyCommittee)
            {
                ResidentMemberHelper.ApplyMemberType(resident, ResidentMemberType.AssociationAdmin);
                resident.AssociationDesignation = designation;
                resident.PropertyOwnerName = null;
                resident.OwnerContactNumber = null;
            }
            else
            {
                resident.AssociationDesignation = designation;
            }

            string? tempPassword = null;
            string? resultPhone = null;

            if (string.IsNullOrEmpty(resident.UserId))
            {
                var provision = await _residentLoginProvisioning.CreateLoginForResidentAsync(
                    resident,
                    phoneOverride: phoneOverride,
                    notificationEmailOverride: notificationEmailOverride);

                if (!provision.Succeeded)
                {
                    return provision;
                }

                resultPhone = provision.LoginPhone;
                tempPassword = provision.TemporaryPassword;
            }
            else
            {
                var user = await _userManager.FindByIdAsync(resident.UserId);
                if (user == null)
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = new[] { "Linked login account was not found." }
                    };
                }

                resultPhone = user.UserName;
            }

            var loginUser = await _userManager.FindByIdAsync(resident.UserId!);
            if (loginUser == null)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Could not load login account after promotion." }
                };
            }

            if (!await _userManager.IsInRoleAsync(loginUser, "Admin"))
            {
                await _userManager.AddToRoleAsync(loginUser, "Admin");
            }

            loginUser.FullName = resident.OwnerName.Trim();
            loginUser.FlatNumber = resident.FlatNumber.Trim();
            loginUser.MustChangePassword = true;
            await _userManager.UpdateAsync(loginUser);

            if (string.IsNullOrEmpty(tempPassword))
            {
                tempPassword = _accountService.GenerateDefaultPassword(resident.FlatNumber);
                var token = await _userManager.GeneratePasswordResetTokenAsync(loginUser);
                var resetResult = await _userManager.ResetPasswordAsync(
                    loginUser,
                    token,
                    tempPassword);

                if (!resetResult.Succeeded)
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = resetResult.Errors.Select(e => e.Description)
                    };
                }
            }

            await _context.SaveChangesAsync();

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = resultPhone ?? LoginIdentityService.NormalizeLoginPhone(resident.PhoneNumber),
                NotificationEmail = string.IsNullOrWhiteSpace(resident.Email) ? null : resident.Email,
                TemporaryPassword = tempPassword
            };
        }

        /// <summary>
        /// Assign or change committee designation for an existing resident (login required for new committee access).
        /// </summary>
        public async Task<ResidentLoginProvisioningResult> ChangeResidentDesignationAsync(
            int residentId,
            string? newDesignation,
            bool removeCommitteeRole)
        {
            var resident = await _context.Residents.FindAsync(residentId);
            if (resident == null)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Resident not found." }
                };
            }

            if (removeCommitteeRole)
            {
                return await DemoteFromCommitteeAsync(resident);
            }

            if (string.IsNullOrWhiteSpace(newDesignation))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Select a committee role or check Remove committee role." }
                };
            }

            var validation = await ValidateDesignationAvailableAsync(
                newDesignation,
                excludeResidentId: resident.Id);

            if (validation != null)
            {
                return validation;
            }

            var designation = AssociationDesignations.All.First(d =>
                d.Equals(newDesignation.Trim(), StringComparison.OrdinalIgnoreCase));

            var wasCommittee = resident.MemberType == ResidentMemberType.AssociationAdmin;
            var sameRole = wasCommittee &&
                string.Equals(
                    resident.AssociationDesignation,
                    designation,
                    StringComparison.OrdinalIgnoreCase);

            if (sameRole)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { $"{designation} is already assigned to this person." }
                };
            }

            if (!wasCommittee)
            {
                if (string.IsNullOrEmpty(resident.UserId))
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = new[]
                        {
                            "Create a login for this resident first, then assign the committee role."
                        }
                    };
                }

                ResidentMemberHelper.ApplyMemberType(resident, ResidentMemberType.AssociationAdmin);
                resident.AssociationDesignation = designation;
                resident.PropertyOwnerName = null;
                resident.OwnerContactNumber = null;
            }
            else
            {
                resident.AssociationDesignation = designation;
            }

            var loginUser = await _userManager.FindByIdAsync(resident.UserId!);
            if (loginUser == null)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Login account not found. Use Repair login on the Residents list." }
                };
            }

            if (!await _userManager.IsInRoleAsync(loginUser, "Admin"))
            {
                await _userManager.AddToRoleAsync(loginUser, "Admin");
            }

            if (!await _userManager.IsInRoleAsync(loginUser, "Resident"))
            {
                await _userManager.AddToRoleAsync(loginUser, "Resident");
            }

            loginUser.FullName = resident.OwnerName.Trim();
            loginUser.FlatNumber = resident.FlatNumber.Trim();
            await _userManager.UpdateAsync(loginUser);

            await _context.SaveChangesAsync();

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = loginUser.UserName,
                NotificationEmail = string.IsNullOrWhiteSpace(resident.Email) ? null : resident.Email
            };
        }

        private async Task<ResidentLoginProvisioningResult> DemoteFromCommitteeAsync(Resident resident)
        {
            if (resident.MemberType != ResidentMemberType.AssociationAdmin)
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "This person is not on the committee." }
                };
            }

            ResidentMemberHelper.ApplyMemberType(resident, ResidentMemberType.Owner);
            resident.AssociationDesignation = null;

            if (!string.IsNullOrEmpty(resident.UserId))
            {
                var loginUser = await _userManager.FindByIdAsync(resident.UserId);
                if (loginUser != null &&
                    !SystemAdminConstants.IsSystemAdmin(loginUser) &&
                    await _userManager.IsInRoleAsync(loginUser, "Admin"))
                {
                    await _userManager.RemoveFromRoleAsync(loginUser, "Admin");
                }
            }

            await _context.SaveChangesAsync();

            return new ResidentLoginProvisioningResult
            {
                Succeeded = true,
                LoginPhone = resident.PhoneNumber
            };
        }

        private async Task<ResidentLoginProvisioningResult?> ValidateDesignationAvailableAsync(
            string designation,
            int? excludeResidentId = null)
        {
            if (!AssociationDesignations.All.Contains(
                    designation,
                    StringComparer.OrdinalIgnoreCase))
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { "Invalid committee designation." }
                };
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation, StringComparison.OrdinalIgnoreCase));

            var takenQuery = _context.Residents.Where(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin &&
                r.AssociationDesignation == designation);

            if (excludeResidentId.HasValue)
            {
                takenQuery = takenQuery.Where(r => r.Id != excludeResidentId.Value);
            }

            if (await takenQuery.AnyAsync())
            {
                return new ResidentLoginProvisioningResult
                {
                    Errors = new[] { $"{designation} is already assigned to someone else." }
                };
            }

            if (!excludeResidentId.HasValue ||
                !await _context.Residents.AnyAsync(r =>
                    r.Id == excludeResidentId.Value &&
                    r.MemberType == ResidentMemberType.AssociationAdmin))
            {
                var committeeCount = await _context.Residents.CountAsync(r =>
                    r.MemberType == ResidentMemberType.AssociationAdmin);

                if (committeeCount >= AssociationDesignations.MaxAssociationAdmins)
                {
                    return new ResidentLoginProvisioningResult
                    {
                        Errors = new[] { "All five committee positions are already filled." }
                    };
                }
            }

            return null;
        }
    }
}
