using ApartmentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class ResidentValidationService
    {
        private readonly ApplicationDbContext _context;

        public ResidentValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsFlatNumberTakenAsync(string flatNumber, int? excludeResidentId = null)
        {
            if (string.IsNullOrWhiteSpace(flatNumber))
            {
                return false;
            }

            var residents = await _context.Residents
                .Select(r => new { r.Id, r.FlatNumber })
                .ToListAsync();

            return residents.Any(r =>
                r.Id != excludeResidentId &&
                FlatNumberHelper.Match(r.FlatNumber, flatNumber));
        }
    }
}
