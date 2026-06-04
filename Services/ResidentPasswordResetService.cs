using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class ResidentPasswordResetService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LoginIdentityService _loginIdentity;

        public ResidentPasswordResetService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            LoginIdentityService loginIdentity)
        {
            _context = context;
            _userManager = userManager;
            _loginIdentity = loginIdentity;
        }

        public async Task<ApplicationUser?> VerifyResidentCredentialsAsync(
            string mobileNumber,
            string flatNumber)
        {
            var loginPhone = LoginIdentityService.NormalizeLoginPhone(mobileNumber);
            if (!LoginIdentityService.IsValidLoginPhone(loginPhone))
            {
                return null;
            }

            var user = await _loginIdentity.FindUserByLoginIdAsync(loginPhone);
            if (user == null || SystemAdminConstants.IsSystemAdmin(user))
            {
                return null;
            }

            if (!await _userManager.IsInRoleAsync(user, "Resident") &&
                !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return null;
            }

            var residents = await _context.Residents.ToListAsync();
            var resident = residents.FirstOrDefault(r => r.UserId == user.Id);

            if (resident == null && !string.IsNullOrWhiteSpace(user.FlatNumber))
            {
                resident = residents.FirstOrDefault(r =>
                    FlatNumberHelper.Match(r.FlatNumber, user.FlatNumber));
            }

            if (resident == null)
            {
                return null;
            }

            if (!FlatNumberHelper.Match(resident.FlatNumber, flatNumber))
            {
                return null;
            }

            if (!PhoneNumberHelper.Match(resident.PhoneNumber, loginPhone) &&
                !PhoneNumberHelper.Match(user.UserName, loginPhone))
            {
                return null;
            }

            return user;
        }
    }
}
