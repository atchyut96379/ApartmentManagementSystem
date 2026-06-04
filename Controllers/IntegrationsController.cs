using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class IntegrationsController : Controller
    {
        private readonly IntegrationStatusService _statusService;
        private readonly PaymentNotificationService _notifications;
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CommitteeAccessService _committeeAccess;

        public IntegrationsController(
            IntegrationStatusService statusService,
            PaymentNotificationService notifications,
            IntegrationsSettingsStore settingsStore,
            UserManager<ApplicationUser> userManager,
            CommitteeAccessService committeeAccess)
        {
            _statusService = statusService;
            _notifications = notifications;
            _settingsStore = settingsStore;
            _userManager = userManager;
            _committeeAccess = committeeAccess;
        }

        public IActionResult Index()
        {
            var appUrl = _settingsStore.GetApplicationSettings().GetAppUrl(Request);
            var status = _statusService.GetStatus(appUrl, "/api/razorpay/webhook");
            var model = _settingsStore.BuildConfigureViewModel(status);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(IntegrationsConfigureViewModel model)
        {
            if (!await CanManageIntegrationsAsync())
            {
                TempData["Error"] = "You do not have permission to save integration settings.";
                return RedirectToAction(nameof(Index));
            }

            if (model.EnableEmail &&
                (string.IsNullOrWhiteSpace(model.SmtpUser) ||
                 string.IsNullOrWhiteSpace(model.FromEmail) ||
                 (string.IsNullOrWhiteSpace(model.SmtpPassword) && !model.PasswordOnFile)))
            {
                TempData["Error"] = "Email: enter SMTP user, from email, and password (Gmail app password).";
                return RedirectToAction(nameof(Index));
            }

            if (model.EnableSms &&
                model.SmsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(model.Msg91SenderId) ||
                 (string.IsNullOrWhiteSpace(model.Msg91AuthKey) && !model.Msg91KeyOnFile)))
            {
                TempData["Error"] = "SMS: enter MSG91 Auth Key and Sender ID.";
                return RedirectToAction(nameof(Index));
            }

            if (model.EnableSms &&
                model.SmsProvider.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] =
                    "Simulation SMS enabled — Remind will succeed but no real text is sent until you add MSG91 keys.";
            }

            if (model.PaymentProvider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase))
            {
                var hasKeyId = !string.IsNullOrWhiteSpace(model.RazorpayKeyId);
                var hasSecret = !string.IsNullOrWhiteSpace(model.RazorpayKeySecret) ||
                                model.RazorpaySecretOnFile;

                if (!hasKeyId || !hasSecret)
                {
                    TempData["Error"] =
                        "Razorpay: enter Key ID and Key Secret, then Save. Get test keys from dashboard.razorpay.com → API Keys.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.AppUrl) ||
                    model.AppUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] =
                        "Razorpay keys saved. For webhooks on a public server, set Public app URL to your HTTPS domain (not localhost).";
                }
            }

            await _settingsStore.SaveAsync(model);

            TempData["Success"] ??=
                "Settings saved to integrations.local.json. Residents will see Pay with Razorpay on checkout — no restart needed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestEmail(string testEmail)
        {
            if (!await CanManageIntegrationsAsync())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(testEmail))
            {
                TempData["Error"] = "Enter an email address for the test.";
                return RedirectToAction(nameof(Index));
            }

            var (ok, err) = await _notifications.SendTestEmailAsync(testEmail.Trim());
            TempData[ok ? "Success" : "Error"] = ok
                ? $"Test email sent to {testEmail}."
                : err ?? "Email send failed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSms(string testPhone)
        {
            if (!await CanManageIntegrationsAsync())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(testPhone))
            {
                TempData["Error"] = "Enter a mobile number for the test.";
                return RedirectToAction(nameof(Index));
            }

            var (ok, err) = await _notifications.SendTestSmsAsync(testPhone.Trim());
            TempData[ok ? "Success" : "Error"] = ok
                ? $"Test SMS sent to {testPhone}."
                : err ?? "SMS send failed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanManageIntegrationsAsync()
        {
            if (!User.IsInRole("Admin"))
            {
                return false;
            }

            if (User.Identity?.Name?.Equals(
                    SystemAdminConstants.UserName,
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null && SystemAdminConstants.IsSystemAdmin(user))
            {
                return true;
            }

            if (user != null)
            {
                return await _committeeAccess.IsCommitteeAdminAsync(user);
            }

            return true;
        }
    }
}
