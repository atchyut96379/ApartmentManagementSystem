using System.Diagnostics;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ResidentProfileService _residentProfileService;
        private readonly DashboardStatsService _dashboardStatsService;

        public HomeController(
            ResidentProfileService residentProfileService,
            DashboardStatsService dashboardStatsService)
        {
            _residentProfileService = residentProfileService;
            _dashboardStatsService = dashboardStatsService;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Resident"))
            {
                var flat = await _residentProfileService.GetFlatForUserAsync(User);
                if (string.IsNullOrEmpty(flat))
                {
                    return RedirectToAction("CompleteProfile", "Account");
                }
            }

            var model = _dashboardStatsService.GetSocietyStats();

            if (User.IsInRole("Admin"))
            {
                model.PendingMaintenanceList =
                    await _dashboardStatsService.GetPendingMaintenancesAsync();
            }

            if (User.IsInRole("Resident"))
            {
                model.ResidentPayments = await BuildResidentPaymentsViewModelAsync(
                    pendingOnly: true,
                    showReceiptColumns: false,
                    returnUrl: "/");
            }

            return View(model);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        private async Task<ResidentPaymentsViewModel> BuildResidentPaymentsViewModelAsync(
            bool pendingOnly,
            bool showReceiptColumns,
            string returnUrl)
        {
            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            var allPayments = string.IsNullOrEmpty(flat)
                ? new List<Maintenance>()
                : await _residentProfileService.GetPaymentsForUserAsync(User, pendingOnly: false);

            var pendingPayments = allPayments.Where(p => !p.PaymentStatus).ToList();

            return new ResidentPaymentsViewModel
            {
                HasResidentProfile = !string.IsNullOrEmpty(flat),
                FlatNumber = flat,
                TotalAssignedCount = allPayments.Count,
                PendingCount = pendingPayments.Count,
                Payments = pendingOnly ? pendingPayments : allPayments,
                PendingOnly = pendingOnly,
                ShowReceiptColumns = showReceiptColumns,
                ReturnUrl = returnUrl
            };
        }
    }
}
