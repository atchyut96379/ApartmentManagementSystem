using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class DashboardStatsService
    {
        private readonly ApplicationDbContext _context;

        public DashboardStatsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public DashboardViewModel GetSocietyStats()
        {
            var currentMonth = DateTime.Now.ToString("MMMM");
            var currentYear = DateTime.Now.Year;

            var totalCollected = _context.Maintenances
                .Where(m => m.PaymentStatus)
                .Sum(m => (decimal?)(m.TotalPaidAmount > 0 ? m.TotalPaidAmount : m.Amount)) ?? 0;

            var model = new DashboardViewModel
            {
                TotalResidents = _context.Residents.Count(),
                TotalFlats = _context.Residents
                    .Select(r => r.FlatNumber)
                    .Distinct()
                    .Count(),
                TotalOwners = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.Owner),
                TotalTenants = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.Tenant),
                TotalAssociationAdmins = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.AssociationAdmin),
                TotalCollected = totalCollected,
                CurrentMonthCollection = _context.Maintenances
                    .Where(m =>
                        m.PaymentStatus &&
                        m.Year == currentYear &&
                        m.Month == currentMonth)
                    .Sum(m => (decimal?)(m.TotalPaidAmount > 0 ? m.TotalPaidAmount : m.Amount)) ?? 0,
                TotalExpenses = _context.Expenses
                    .Sum(e => (decimal?)e.Amount) ?? 0,
                PendingPayments = _context.Maintenances
                    .Count(m => !m.PaymentStatus),
                TotalMaintenanceRecords = _context.Maintenances.Count()
            };

            model.BalanceAmount = model.TotalCollected - model.TotalExpenses;
            return model;
        }

        public async Task<List<Maintenance>> GetPendingMaintenancesAsync()
        {
            return await _context.Maintenances
                .Where(m => !m.PaymentStatus)
                .OrderBy(m => m.FlatNumber)
                .ThenByDescending(m => m.Year)
                .ToListAsync();
        }

        public async Task<List<AssociationMemberViewModel>> GetAssociationCommitteeAsync()
        {
            return await _context.Residents
                .Where(r => r.MemberType == ResidentMemberType.AssociationAdmin)
                .OrderBy(r => r.AssociationDesignation)
                .Select(r => new AssociationMemberViewModel
                {
                    FlatNumber = r.FlatNumber,
                    Name = r.OwnerName,
                    Designation = r.AssociationDesignation ?? "",
                    PhoneNumber = r.PhoneNumber,
                    Email = r.Email
                })
                .ToListAsync();
        }

        public async Task<List<string>> GetFlatsWithoutMaintenanceAsync()
        {
            var residents = await _context.Residents.ToListAsync();
            var maintenances = await _context.Maintenances.ToListAsync();

            return residents
                .Where(r => !maintenances.Any(m =>
                    FlatNumberHelper.Match(m.FlatNumber, r.FlatNumber)))
                .Select(r => r.FlatNumber)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
        }
    }
}
