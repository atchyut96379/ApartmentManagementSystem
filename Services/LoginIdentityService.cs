using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class LoginIdentityService
    {
        private const int MinLoginPhoneDigits = 10;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LoginIdentityService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public static string NormalizeLoginPhone(string? phone)
        {
            var digits = PhoneNumberHelper.NormalizeDigits(phone);
            if (digits.Length > 10 && digits.StartsWith("91", StringComparison.Ordinal))
            {
                digits = digits[2..];
            }

            return digits;
        }

        public static bool IsValidLoginPhone(string? phone)
        {
            return NormalizeLoginPhone(phone).Length >= MinLoginPhoneDigits;
        }

        public static string BuildInternalEmail(string loginPhone, string domain = "login.local")
        {
            return $"{loginPhone}@{domain.Trim().ToLowerInvariant()}";
        }

        public async Task<ApplicationUser?> FindUserByLoginIdAsync(string loginId)
        {
            if (string.IsNullOrWhiteSpace(loginId))
            {
                return null;
            }

            var trimmed = loginId.Trim();

            if (string.Equals(trimmed, SystemAdminConstants.UserName, StringComparison.OrdinalIgnoreCase))
            {
                return await _userManager.FindByNameAsync(SystemAdminConstants.UserName);
            }

            var phone = NormalizeLoginPhone(trimmed);
            if (phone.Length >= MinLoginPhoneDigits)
            {
                var byPhone = await _userManager.FindByNameAsync(phone);
                if (byPhone != null)
                {
                    return byPhone;
                }

                var users = await _userManager.Users.ToListAsync();
                byPhone = users.FirstOrDefault(u =>
                    PhoneNumberHelper.Match(u.UserName, phone));
                if (byPhone != null)
                {
                    return byPhone;
                }
            }

            var byName = await _userManager.FindByNameAsync(trimmed);
            if (byName != null)
            {
                return byName;
            }

            var byEmail = await _userManager.FindByEmailAsync(trimmed);
            if (byEmail != null)
            {
                return byEmail;
            }

            if (phone.Length >= MinLoginPhoneDigits)
            {
                var residentUser = await FindUserViaResidentPhoneAsync(phone);
                if (residentUser != null)
                {
                    return residentUser;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds identity user linked to a resident phone (including legacy email-as-username accounts).
        /// </summary>
        public async Task<ApplicationUser?> FindUserViaResidentPhoneAsync(string loginPhone)
        {
            var normalized = NormalizeLoginPhone(loginPhone);
            if (normalized.Length < MinLoginPhoneDigits)
            {
                return null;
            }

            var residents = await _context.Residents.ToListAsync();
            var resident = residents.FirstOrDefault(r =>
                PhoneNumberHelper.Match(r.PhoneNumber, normalized));

            if (resident == null)
            {
                return null;
            }

            return await FindUserForResidentAsync(resident);
        }

        public async Task<ApplicationUser?> FindUserForResidentAsync(Resident resident)
        {
            if (!string.IsNullOrEmpty(resident.UserId))
            {
                var byId = await _userManager.FindByIdAsync(resident.UserId);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(resident.Email))
            {
                var email = resident.Email.Trim();
                var byEmail = await _userManager.FindByEmailAsync(email);
                if (byEmail != null)
                {
                    return byEmail;
                }

                var byName = await _userManager.FindByNameAsync(email);
                if (byName != null)
                {
                    return byName;
                }
            }

            var loginPhone = NormalizeLoginPhone(resident.PhoneNumber);
            if (loginPhone.Length >= MinLoginPhoneDigits)
            {
                var byPhone = await _userManager.FindByNameAsync(loginPhone);
                if (byPhone != null)
                {
                    return byPhone;
                }

                var users = await _userManager.Users.ToListAsync();
                return users.FirstOrDefault(u =>
                    !SystemAdminConstants.IsSystemAdmin(u) &&
                    PhoneNumberHelper.Match(u.UserName, loginPhone));
            }

            return null;
        }

        public async Task<bool> ClearOrphanResidentUserIdAsync(Resident resident)
        {
            if (string.IsNullOrEmpty(resident.UserId))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(resident.UserId);
            if (user != null)
            {
                return false;
            }

            resident.UserId = null;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsLoginPhoneTakenAsync(string loginPhone, string? excludeUserId = null)
        {
            var normalized = NormalizeLoginPhone(loginPhone);
            if (normalized.Length < MinLoginPhoneDigits)
            {
                return false;
            }

            var existing = await FindUserByLoginIdAsync(normalized);
            if (existing == null)
            {
                return false;
            }

            return excludeUserId == null || existing.Id != excludeUserId;
        }
    }
}
