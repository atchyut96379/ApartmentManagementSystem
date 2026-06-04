using ApartmentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class SocietyDataResetService
    {
        private readonly ApplicationDbContext _context;

        public SocietyDataResetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ClearAllSocietyDataAsync()
        {
            _context.Maintenances.RemoveRange(await _context.Maintenances.ToListAsync());
            _context.Expenses.RemoveRange(await _context.Expenses.ToListAsync());
            _context.Residents.RemoveRange(await _context.Residents.ToListAsync());
            await _context.SaveChangesAsync();
        }
    }
}
