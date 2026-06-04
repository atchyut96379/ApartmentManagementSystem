using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class DashboardStatsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardStatsService> _logger;

        public DashboardStatsService(
            ApplicationDbContext context,
            ILogger<DashboardStatsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public DashboardViewModel GetSocietyStats()
        {
            try
            {
                return BuildSocietyStats();
            }
            catch (Exception ex) when (IsSchemaException(ex))
            {
                _logger.LogWarning(ex, "Dashboard stats using legacy schema fallback.");
                return BuildLegacySocietyStats();
            }
        }

        private DashboardViewModel BuildSocietyStats()
        {
            var currentMonth = DateTime.Now.ToString("MMMM");
            var currentYear = DateTime.Now.Year;

            var totalCollected = SumPaidAmount(_context.Maintenances.Where(m => m.PaymentStatus));

            var model = new DashboardViewModel
            {
                TotalResidents = _context.Residents.Count(),
                TotalFlats = _context.Residents
                    .Select(r => r.FlatNumber)
                    .Distinct()
                    .Count(),
                TotalOwners = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.Owner ||
                    r.MemberType == ResidentMemberType.AssociationAdmin),
                TotalTenants = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.Tenant),
                TotalAssociationAdmins = _context.Residents.Count(r =>
                    r.MemberType == ResidentMemberType.AssociationAdmin),
                TotalCollected = totalCollected,
                CurrentMonthCollection = SumPaidAmount(
                    _context.Maintenances.Where(m =>
                        m.PaymentStatus &&
                        m.Year == currentYear &&
                        m.Month == currentMonth)),
                TotalExpenses = _context.Expenses
                    .Sum(e => (decimal?)e.Amount) ?? 0,
                PendingPayments = _context.Maintenances
                    .Count(m => !m.PaymentStatus),
                TotalMaintenanceRecords = _context.Maintenances.Count()
            };

            model.BalanceAmount = model.TotalCollected - model.TotalExpenses;
            return model;
        }

        private DashboardViewModel BuildLegacySocietyStats()
        {
            var currentMonth = DateTime.Now.ToString("MMMM");
            var currentYear = DateTime.Now.Year;

            var totalCollected = _context.Maintenances
                .Where(m => m.PaymentStatus)
                .Sum(m => (decimal?)m.Amount) ?? 0;

            var model = new DashboardViewModel
            {
                TotalResidents = _context.Residents.Count(),
                TotalFlats = _context.Residents
                    .Select(r => r.FlatNumber)
                    .Distinct()
                    .Count(),
                TotalOwners = _context.Residents.Count(r => r.IsOwner),
                TotalTenants = _context.Residents.Count(r => !r.IsOwner),
                TotalAssociationAdmins = 0,
                TotalCollected = totalCollected,
                CurrentMonthCollection = _context.Maintenances
                    .Where(m =>
                        m.PaymentStatus &&
                        m.Year == currentYear &&
                        m.Month == currentMonth)
                    .Sum(m => (decimal?)m.Amount) ?? 0,
                TotalExpenses = _context.Expenses
                    .Sum(e => (decimal?)e.Amount) ?? 0,
                PendingPayments = _context.Maintenances
                    .Count(m => !m.PaymentStatus),
                TotalMaintenanceRecords = _context.Maintenances.Count()
            };

            model.BalanceAmount = model.TotalCollected - model.TotalExpenses;
            return model;
        }

        private decimal SumPaidAmount(IQueryable<Maintenance> paid)
        {
            try
            {
                return paid.Sum(m => (decimal?)(m.TotalPaidAmount > 0 ? m.TotalPaidAmount : m.Amount)) ?? 0;
            }
            catch (Exception ex) when (IsSchemaException(ex))
            {
                return paid.Sum(m => (decimal?)m.Amount) ?? 0;
            }
        }

        public async Task<List<Maintenance>> GetPendingMaintenancesAsync()
        {
            try
            {
                return await _context.Maintenances
                    .Where(m => !m.PaymentStatus)
                    .OrderBy(m => m.FlatNumber)
                    .ThenByDescending(m => m.Year)
                    .ToListAsync();
            }
            catch (Exception ex) when (IsSchemaException(ex))
            {
                _logger.LogWarning(ex, "Pending maintenance list unavailable until migrations run.");
                return new List<Maintenance>();
            }
        }

        public async Task<List<AssociationMemberViewModel>> GetAssociationCommitteeAsync()
        {
            try
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
            catch (Exception ex) when (IsSchemaException(ex))
            {
                _logger.LogWarning(ex, "Committee list unavailable until migrations run.");
                return new List<AssociationMemberViewModel>();
            }
        }

        public async Task<List<string>> GetFlatsWithoutMaintenanceAsync()
        {
            try
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
            catch (Exception ex) when (IsSchemaException(ex))
            {
                _logger.LogWarning(ex, "Flats-without-maintenance check skipped.");
                return new List<string>();
            }
        }

        private static bool IsSchemaException(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
