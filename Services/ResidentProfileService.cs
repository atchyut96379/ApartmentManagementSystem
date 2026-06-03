using System.Security.Claims;
using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class ResidentProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MaintenanceBillingService _billingService;

        public ResidentProfileService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MaintenanceBillingService billingService)
        {
            _context = context;
            _userManager = userManager;
            _billingService = billingService;
        }

        public async Task<Resident?> GetCurrentResidentAsync(ClaimsPrincipal principal)
        {
            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
            {
                return null;
            }

            return await EnsureResidentProfileAsync(user);
        }

        public async Task<Resident?> LinkResidentForUserAsync(ApplicationUser user)
        {
            return await EnsureResidentProfileAsync(user);
        }

        public async Task<string?> GetFlatForUserAsync(ClaimsPrincipal principal)
        {
            var resident = await GetCurrentResidentAsync(principal);
            if (resident != null)
            {
                return resident.FlatNumber.Trim();
            }

            var user = await _userManager.GetUserAsync(principal);
            return user?.FlatNumber?.Trim();
        }

        public async Task<List<Maintenance>> GetPaymentsForUserAsync(
            ClaimsPrincipal principal,
            bool pendingOnly = false)
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
            {
                return new List<Maintenance>();
            }

            var resident = await EnsureResidentProfileAsync(user);
            var flat = resident?.FlatNumber.Trim() ?? user.FlatNumber?.Trim();

            if (string.IsNullOrEmpty(flat))
            {
                return new List<Maintenance>();
            }

            return await GetPaymentsByFlatAsync(flat, pendingOnly);
        }

        public async Task<List<Maintenance>> GetPaymentsAsync(
            Resident resident,
            bool pendingOnly = false)
        {
            return await GetPaymentsByFlatAsync(resident.FlatNumber.Trim(), pendingOnly);
        }

        public async Task<Resident?> EnsureResidentProfileAsync(ApplicationUser user)
        {
            var resident = await FindAndLinkResidentAsync(user);
            if (resident != null)
            {
                return resident;
            }

            var email = (user.Email ?? user.UserName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            var unlinkedResidents = await _context.Residents
                .Where(r => r.UserId == null || r.UserId == string.Empty)
                .ToListAsync();

            resident = unlinkedResidents.FirstOrDefault(r =>
                !string.IsNullOrWhiteSpace(r.Email) &&
                Normalize(r.Email) == Normalize(email));

            if (resident == null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                var name = user.FullName.Trim();
                resident = unlinkedResidents.FirstOrDefault(r =>
                    r.OwnerName.Trim()
                        .Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            if (resident != null)
            {
                return await LinkResidentToUserAsync(resident, user);
            }

            if (!string.IsNullOrWhiteSpace(user.FlatNumber))
            {
                return await CreateResidentForUserAsync(
                    user,
                    user.FlatNumber.Trim(),
                    email);
            }

            return null;
        }

        public async Task<Resident> CreateResidentForUserAsync(
            ApplicationUser user,
            string flatNumber,
            string? email = null)
        {
            email ??= (user.Email ?? user.UserName ?? string.Empty).Trim();

            var existing = await _context.Residents
                .FirstOrDefaultAsync(r => r.UserId == user.Id);

            if (existing != null)
            {
                return existing;
            }

            var resident = new Resident
            {
                FlatNumber = flatNumber.Trim(),
                OwnerName = string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName.Trim(),
                Email = email,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                UserId = user.Id,
                IsOwner = true,
                CreatedDate = DateTime.Now
            };

            _context.Residents.Add(resident);

            user.FlatNumber = flatNumber.Trim();
            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            return resident;
        }

        private async Task<List<Maintenance>> GetPaymentsByFlatAsync(
            string flat,
            bool pendingOnly)
        {
            var maintenances = await _context.Maintenances.ToListAsync();

            var query = maintenances
                .Where(m => FlatNumberHelper.Match(m.FlatNumber, flat));

            if (pendingOnly)
            {
                query = query.Where(m => !m.PaymentStatus);
            }

            return query
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Id)
                .ToList();
        }

        private async Task<Resident?> FindAndLinkResidentAsync(ApplicationUser user)
        {
            var normalizedEmail = Normalize(user.Email ?? user.UserName);

            Resident? resident = null;

            if (!string.IsNullOrEmpty(user.Id))
            {
                resident = await _context.Residents
                    .FirstOrDefaultAsync(r => r.UserId == user.Id);
            }

            if (resident == null && !string.IsNullOrEmpty(normalizedEmail))
            {
                var residents = await _context.Residents.ToListAsync();

                resident = residents.FirstOrDefault(r =>
                    !string.IsNullOrWhiteSpace(r.Email) &&
                    Normalize(r.Email) == normalizedEmail);
            }

            if (resident == null && !string.IsNullOrWhiteSpace(user.FlatNumber))
            {
                var flat = user.FlatNumber.Trim();
                var residents = await _context.Residents.ToListAsync();
                resident = residents.FirstOrDefault(r =>
                    r.FlatNumber.Trim()
                        .Equals(flat, StringComparison.OrdinalIgnoreCase));
            }

            if (resident == null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                var name = user.FullName.Trim();
                var residents = await _context.Residents.ToListAsync();
                resident = residents.FirstOrDefault(r =>
                    r.OwnerName.Trim()
                        .Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            if (resident == null)
            {
                return null;
            }

            return await LinkResidentToUserAsync(resident, user);
        }

        private async Task<Resident> LinkResidentToUserAsync(
            Resident resident,
            ApplicationUser user)
        {
            var changed = false;

            if (string.IsNullOrEmpty(resident.UserId))
            {
                resident.UserId = user.Id;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(resident.Email) &&
                !string.IsNullOrWhiteSpace(user.Email))
            {
                resident.Email = user.Email.Trim();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.FlatNumber))
            {
                user.FlatNumber = resident.FlatNumber.Trim();
                await _userManager.UpdateAsync(user);
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }

            return resident;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().ToLowerInvariant();
        }
    }
}
