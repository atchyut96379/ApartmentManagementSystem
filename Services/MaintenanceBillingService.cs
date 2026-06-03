using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class MaintenanceBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public MaintenanceBillingService(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public decimal DefaultMonthlyAmount =>
            _configuration.GetValue("Maintenance:DefaultMonthlyAmount", 1200m);

        public async Task EnsureMonthlyMaintenanceForAllResidentsAsync()
        {
            await NormalizeFlatNumbersAsync();

            var residents = await _context.Residents.ToListAsync();
            if (residents.Count == 0)
            {
                return;
            }

            var month = DateTime.Now.ToString("MMMM");
            var year = DateTime.Now.Year;
            var amount = DefaultMonthlyAmount;
            var dueDate = new DateTime(year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);

            var existing = await _context.Maintenances.ToListAsync();
            var added = false;

            foreach (var resident in residents)
            {
                var flat = resident.FlatNumber.Trim();

                var alreadyExists = existing.Any(m =>
                    FlatNumberHelper.Match(m.FlatNumber, flat) &&
                    m.Year == year &&
                    string.Equals(m.Month, month, StringComparison.OrdinalIgnoreCase));

                if (alreadyExists)
                {
                    continue;
                }

                var maintenance = new Maintenance
                {
                    FlatNumber = flat,
                    Amount = amount,
                    Month = month,
                    Year = year,
                    DueDate = dueDate,
                    PaymentStatus = false,
                    PaymentDate = null,
                    PaidDate = null,
                    ReceiptNumber = null,
                    Remarks = "Monthly maintenance"
                };

                _context.Maintenances.Add(maintenance);
                existing.Add(maintenance);
                added = true;
            }

            if (added)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task NormalizeFlatNumbersAsync()
        {
            var changed = false;

            foreach (var resident in await _context.Residents.ToListAsync())
            {
                var trimmed = resident.FlatNumber.Trim();
                if (resident.FlatNumber != trimmed)
                {
                    resident.FlatNumber = trimmed;
                    changed = true;
                }
            }

            foreach (var maintenance in await _context.Maintenances.ToListAsync())
            {
                var trimmed = maintenance.FlatNumber.Trim();
                if (maintenance.FlatNumber != trimmed)
                {
                    maintenance.FlatNumber = trimmed;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
