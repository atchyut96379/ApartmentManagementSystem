using System.Diagnostics;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ResidentProfileService residentProfileService,
            DashboardStatsService dashboardStatsService,
            UserManager<ApplicationUser> userManager,
            DashboardReportExportService reportExportService,
            IOptions<SocietySettings> society,
            IOptions<ApplicationSettings> app,
            ILogger<HomeController> logger,
            IWebHostEnvironment environment)
        {
            _residentProfileService = residentProfileService;
            _dashboardStatsService = dashboardStatsService;
            _userManager = userManager;
            _reportExportService = reportExportService;
            _society = society.Value;
            _app = app.Value;
            _logger = logger;
            _environment = environment;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                return await IndexCoreAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard failed to load.");
                return View(
                    "Error",
                    new ErrorViewModel
                    {
                        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                        UserMessage = BuildErrorUserMessage(ex)
                    });
            }
        }

        private async Task<IActionResult> IndexCoreAsync()
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
            else if (User.IsInRole("Admin") && !SystemAdminConstants.IsSystemAdmin(user))
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
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var error = exceptionFeature?.Error;

            if (error != null)
            {
                _logger.LogError(
                    error,
                    "Unhandled error. Path={Path}",
                    exceptionFeature?.Path);
            }

            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                UserMessage = BuildErrorUserMessage(error)
            };

            return View(model);
        }

        private string? BuildErrorUserMessage(Exception? error)
        {
            if (error == null)
            {
                return null;
            }

            var root = error;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            if (root is SqlException or DbUpdateException)
            {
                return "Database schema is out of date. Restart the app after deploy (migrations run automatically). "
                    + "If this continues, set DATABASE_MIGRATE_ON_STARTUP=true in Azure and restart once.";
            }

            if (_environment.IsDevelopment() || User.IsInRole("Admin"))
            {
                return root.Message;
            }

            return "Something went wrong. Try again or contact the system administrator.";
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
