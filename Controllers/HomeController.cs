using System.Diagnostics;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ResidentProfileService _residentProfileService;
        private readonly DashboardStatsService _dashboardStatsService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DashboardReportExportService _reportExportService;
        private readonly SocietySettings _society;
        private readonly ApplicationSettings _app;

        public HomeController(
            ResidentProfileService residentProfileService,
            DashboardStatsService dashboardStatsService,
            UserManager<ApplicationUser> userManager,
            DashboardReportExportService reportExportService,
            IOptions<SocietySettings> society,
            IOptions<ApplicationSettings> app)
        {
            _residentProfileService = residentProfileService;
            _dashboardStatsService = dashboardStatsService;
            _userManager = userManager;
            _reportExportService = reportExportService;
            _society = society.Value;
            _app = app.Value;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.MustChangePassword == true)
            {
                return RedirectToAction("ChangePasswordRequired", "Account");
            }

            if (User.IsInRole("Resident"))
            {
                var flat = await _residentProfileService.GetFlatForUserAsync(User);
                if (string.IsNullOrEmpty(flat))
                {
                    return RedirectToAction("CompleteProfile", "Account");
                }
            }

            var showSocietyOverview =
                User.IsInRole("Admin") || User.IsInRole("Resident");

            var model = showSocietyOverview
                ? _dashboardStatsService.GetSocietyStats()
                : new DashboardViewModel();

            if (showSocietyOverview)
            {
                model.PendingMaintenanceList =
                    await _dashboardStatsService.GetPendingMaintenancesAsync();
                model.AssociationCommittee =
                    await _dashboardStatsService.GetAssociationCommitteeAsync();
            }

            if (User.IsInRole("Resident"))
            {
                model.ResidentPayments = await BuildResidentPaymentsViewModelAsync(
                    pendingOnly: true,
                    showReceiptColumns: false,
                    returnUrl: "/");
            }
            else if (User.IsInRole("Admin"))
            {
                var payments = await BuildResidentPaymentsViewModelAsync(
                    pendingOnly: true,
                    showReceiptColumns: false,
                    returnUrl: "/");

                if (payments.HasResidentProfile)
                {
                    model.ResidentPayments = payments;
                }
            }

            return View(model);
        }

        [Authorize(Roles = "Admin,Resident")]
        public async Task<IActionResult> DownloadExcelReport()
        {
            var bytes = await _reportExportService.BuildDashboardWorkbookAsync();
            var fileName = $"ApartmentDashboard_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [AllowAnonymous]
        public IActionResult Public()
        {
            return View(BuildPublicSiteModel());
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View(BuildPublicSiteModel());
        }

        [AllowAnonymous]
        public IActionResult Terms()
        {
            return View(BuildPublicSiteModel());
        }

        [AllowAnonymous]
        public IActionResult Refund()
        {
            return View(BuildPublicSiteModel());
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View(BuildPublicSiteModel());
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

        private PublicSiteViewModel BuildPublicSiteModel()
        {
            var baseUrl = !string.IsNullOrWhiteSpace(_app.AppUrl)
                ? _app.AppUrl.TrimEnd('/')
                : $"{Request.Scheme}://{Request.Host}";

            return new PublicSiteViewModel
            {
                ApartmentName = _society.ApartmentName,
                ContactEmail = _society.ContactEmail,
                ContactPhone = _society.ContactPhone,
                Address = _society.Address,
                AppBaseUrl = baseUrl
            };
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
