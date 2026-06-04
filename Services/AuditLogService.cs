using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string action,
            string details,
            string? entityType = null,
            int? entityId = null,
            string? flatNumber = null,
            ApplicationUser? actor = null)
        {
            actor ??= await _userManager.GetUserAsync(
                _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal());

            var displayName = actor?.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = actor?.UserName ?? "System";
            }

            if (actor != null && SystemAdminConstants.IsSystemAdmin(actor))
            {
                displayName = "System Admin";
            }

            _context.AuditLogs.Add(new AuditLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                ActorUserId = actor?.Id,
                ActorDisplayName = displayName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                FlatNumber = flatNumber?.Trim(),
                Details = details.Length > 2000 ? details[..2000] : details
            });

            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
        {
            return await _context.AuditLogs
                .OrderByDescending(a => a.CreatedAtUtc)
                .ThenByDescending(a => a.Id)
                .Take(count)
                .ToListAsync();
        }
    }
}
