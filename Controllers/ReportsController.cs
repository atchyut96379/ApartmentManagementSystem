using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Resident")]
    public class ReportsController : Controller
    {
        private readonly DashboardStatsService _dashboardStatsService;
        private readonly DashboardReportExportService _reportExportService;

        public ReportsController(
            DashboardStatsService dashboardStatsService,
            DashboardReportExportService reportExportService)
        {
            _dashboardStatsService = dashboardStatsService;
            _reportExportService = reportExportService;
        }

        public async Task<IActionResult> Index()
        {
            var model = _dashboardStatsService.GetSocietyStats();
            model.PendingMaintenanceList =
                await _dashboardStatsService.GetPendingMaintenancesAsync();
            model.AssociationCommittee =
                await _dashboardStatsService.GetAssociationCommitteeAsync();
            return View(model);
        }

        public async Task<IActionResult> DownloadExcel()
        {
            var bytes = await _reportExportService.BuildDashboardWorkbookAsync();
            var fileName = $"ApartmentReport_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
