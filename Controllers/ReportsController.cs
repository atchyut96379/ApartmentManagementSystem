using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly DashboardStatsService _dashboardStatsService;

        public ReportsController(DashboardStatsService dashboardStatsService)
        {
            _dashboardStatsService = dashboardStatsService;
        }

        public async Task<IActionResult> Index()
        {
            var model = _dashboardStatsService.GetSocietyStats();
            model.PendingMaintenanceList =
                await _dashboardStatsService.GetPendingMaintenancesAsync();
            return View(model);
        }
    }
}
