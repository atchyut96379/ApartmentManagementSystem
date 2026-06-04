using ApartmentManagementSystem.Filters;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    [RequireCommitteeAdmin]
    public class AuditLogController : Controller
    {
        private readonly AuditLogService _auditLog;

        public AuditLogController(AuditLogService auditLog)
        {
            _auditLog = auditLog;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _auditLog.GetRecentAsync(200);
            return View(logs);
        }
    }
}
