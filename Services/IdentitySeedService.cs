using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class IdentitySeedService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IdentitySeedService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task EnsureRolesAsync()
        {
            foreach (var role in new[] { "Admin", "Resident" })
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        public async Task ResetAllUsersAndSeedSystemAdminAsync(
            SocietyDataResetService? societyReset = null)
        {
            await EnsureRolesAsync();

            if (societyReset != null)
            {
                await societyReset.ClearAllSocietyDataAsync();
            }

            foreach (var resident in await _context.Residents.ToListAsync())
            {
                resident.UserId = null;
            }

            await _context.SaveChangesAsync();

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUserTokens]");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUserLogins]");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUserClaims]");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUserRoles]");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUsers]");

            var admin = new ApplicationUser
            {
                UserName = SystemAdminConstants.UserName,
                Email = SystemAdminConstants.Email,
                FullName = SystemAdminConstants.FullName,
                EmailConfirmed = true,
                MustChangePassword = false,
                FlatNumber = null
            };

            var createResult = await _userManager.CreateAsync(
                admin,
                SystemAdminConstants.DefaultPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create system admin: " +
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(admin, "Admin");
        }

        public async Task EnsureSystemAdminExistsAsync()
        {
            await EnsureRolesAsync();

            var admin = await _userManager.FindByNameAsync(SystemAdminConstants.UserName);
            if (admin != null)
            {
                return;
            }

            admin = new ApplicationUser
            {
                UserName = SystemAdminConstants.UserName,
                Email = SystemAdminConstants.Email,
                FullName = SystemAdminConstants.FullName,
                EmailConfirmed = true,
                MustChangePassword = false
            };

            var createResult = await _userManager.CreateAsync(
                admin,
                SystemAdminConstants.DefaultPassword);

            if (!createResult.Succeeded)
            {
                return;
            }

            await _userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
