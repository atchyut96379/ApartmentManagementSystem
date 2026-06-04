using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApartmentManagementSystem.Services
{
    public class CommitteeAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommitteeAccessService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return await _userManager.GetUserAsync(principal);
        }

        public bool IsSystemAdmin(ApplicationUser? user)
        {
            return SystemAdminConstants.IsSystemAdmin(user);
        }

        public async Task<bool> IsCommitteeAdminAsync(ClaimsPrincipal principal)
        {
            var user = await _userManager.GetUserAsync(principal);
            return await IsCommitteeAdminAsync(user);
        }

        public async Task<bool> IsCommitteeAdminAsync(ApplicationUser? user)
        {
            if (user == null || SystemAdminConstants.IsSystemAdmin(user))
            {
                return false;
            }

            if (!await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return false;
            }

            return await _context.Residents.AnyAsync(r =>
                r.UserId == user.Id &&
                r.MemberType == ResidentMemberType.AssociationAdmin);
        }

        public async Task<bool> CanManageResidentsAsync(ClaimsPrincipal principal)
        {
            return await IsCommitteeAdminAsync(principal);
        }

        public async Task<Resident?> GetCommitteeProfileAsync(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            return await _context.Residents.FirstOrDefaultAsync(r =>
                r.UserId == user.Id &&
                r.MemberType == ResidentMemberType.AssociationAdmin);
        }

        /// <summary>Only President or Secretary may change committee designations on the Residents list.</summary>
        public async Task<bool> CanChangeCommitteeDesignationAsync(ClaimsPrincipal principal)
        {
            var user = await GetUserAsync(principal);
            return await CanChangeCommitteeDesignationAsync(user);
        }

        public async Task<bool> CanChangeCommitteeDesignationAsync(ApplicationUser? user)
        {
            if (user == null || IsSystemAdmin(user))
            {
                return false;
            }

            var profile = await GetCommitteeProfileAsync(user);
            return AssociationDesignations.CanChangeMemberDesignations(
                profile?.AssociationDesignation);
        }
    }
}
