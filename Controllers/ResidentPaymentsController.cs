using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Resident,Admin")]
    public class ResidentPaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ResidentProfileService _residentProfileService;
        private readonly MaintenanceBillingService _billingService;
        private readonly PaymentGatewayService _paymentGateway;
        private readonly PaymentReceiptPdfService _receiptPdf;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IntegrationsSettingsStore _integrationsStore;

        public ResidentPaymentsController(
            ApplicationDbContext context,
            ResidentProfileService residentProfileService,
            MaintenanceBillingService billingService,
            PaymentGatewayService paymentGateway,
            PaymentReceiptPdfService receiptPdf,
            UserManager<ApplicationUser> userManager,
            IntegrationsSettingsStore integrationsStore)
        {
            _context = context;
            _residentProfileService = residentProfileService;
            _billingService = billingService;
            _paymentGateway = paymentGateway;
            _receiptPdf = receiptPdf;
            _userManager = userManager;
            _integrationsStore = integrationsStore;
        }

        public async Task<IActionResult> MyPayments()
        {
            var redirect = await GetAccessRedirectAsync();
            if (redirect != null)
            {
                return redirect;
            }

            return View(await BuildPaymentsViewModelAsync(
                pendingOnly: false,
                showReceiptColumns: true,
                returnUrl: "/ResidentPayments/MyPayments"));
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int id, string? returnUrl)
        {
            var redirect = await GetAccessRedirectAsync();
            if (redirect != null)
            {
                return redirect;
            }

            var maintenance = await GetPayableMaintenanceAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            if (maintenance.PaymentStatus)
            {
                return RedirectToAction(nameof(Receipt), new { id });
            }

            var resident = await _residentProfileService.GetCurrentResidentAsync(User);
            var payerName = resident?.OwnerName
                ?? (await _userManager.GetUserAsync(User))?.FullName
                ?? "Resident";

            var model = await _paymentGateway.BuildCheckoutAsync(
                maintenance,
                payerName,
                returnUrl ?? "/ResidentPayments/MyPayments");

            if (model == null)
            {
                return RedirectToAction(nameof(Receipt), new { id });
            }

            ViewBag.IntegrationsConfigured = _integrationsStore.LocalFileExists;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteSimulation(int id, string? returnUrl)
        {
            if (_paymentGateway.IsLiveRazorpayEnabled())
            {
                TempData["Error"] =
                    "Live Razorpay is enabled. Use the Pay with Razorpay button — simulation is disabled.";
                return RedirectToAction(nameof(Checkout), new { id, returnUrl });
            }

            var redirect = await GetAccessRedirectAsync();
            if (redirect != null)
            {
                return redirect;
            }

            var maintenance = await GetPayableMaintenanceAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            var resident = await _residentProfileService.GetCurrentResidentAsync(User);
            var payerName = resident?.OwnerName
                ?? (await _userManager.GetUserAsync(User))?.FullName
                ?? "Resident";

            var result = await _paymentGateway.CompleteSimulatedPaymentAsync(id, payerName);
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error ?? "Payment could not be completed.";
                return RedirectToAction(nameof(Checkout), new { id, returnUrl });
            }

            TempData["Success"] = "Payment completed successfully.";
            return RedirectToAction(nameof(Receipt), new { id = result.MaintenanceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRazorpay(
            int id,
            string razorpay_order_id,
            string razorpay_payment_id,
            string razorpay_signature,
            string? returnUrl)
        {
            var redirect = await GetAccessRedirectAsync();
            if (redirect != null)
            {
                return redirect;
            }

            var maintenance = await GetPayableMaintenanceAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            var resident = await _residentProfileService.GetCurrentResidentAsync(User);
            var payerName = resident?.OwnerName
                ?? (await _userManager.GetUserAsync(User))?.FullName
                ?? "Resident";

            var result = await _paymentGateway.CompleteRazorpayPaymentAsync(
                id,
                payerName,
                razorpay_order_id,
                razorpay_payment_id,
                razorpay_signature);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error ?? "Payment verification failed.";
                return RedirectToAction(nameof(Checkout), new { id, returnUrl });
            }

            TempData["Success"] = "Payment completed successfully.";
            return RedirectToAction(nameof(Receipt), new { id = result.MaintenanceId });
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            if (!await CanViewMaintenanceAsync(maintenance))
            {
                TempData["Error"] = "You can only view receipts for your own flat.";
                return RedirectToAction("Index", "Home");
            }

            var receipt = _paymentGateway.BuildReceipt(maintenance);
            if (receipt == null)
            {
                TempData["Error"] = "Receipt is available only after a successful online payment.";
                return RedirectToAction(nameof(MyPayments));
            }

            return View(receipt);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            if (!await CanViewMaintenanceAsync(maintenance))
            {
                TempData["Error"] = "You can only download receipts for your own flat.";
                return RedirectToAction("Index", "Home");
            }

            var receipt = _paymentGateway.BuildReceipt(maintenance);
            if (receipt == null)
            {
                TempData["Error"] = "Receipt is available only after a successful online payment.";
                return RedirectToAction(nameof(MyPayments));
            }

            var pdf = _receiptPdf.GeneratePdf(receipt);
            var fileName = $"receipt-{receipt.ReceiptNumber}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        private async Task<Maintenance?> GetPayableMaintenanceAsync(int id)
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            if (string.IsNullOrEmpty(flat))
            {
                return null;
            }

            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance == null || !FlatNumberHelper.Match(maintenance.FlatNumber, flat))
            {
                return null;
            }

            return maintenance;
        }

        private async Task<bool> CanViewMaintenanceAsync(Maintenance maintenance)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            return !string.IsNullOrEmpty(flat) &&
                   FlatNumberHelper.Match(maintenance.FlatNumber, flat);
        }

        private async Task<IActionResult?> GetAccessRedirectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (SystemAdminConstants.IsSystemAdmin(user))
            {
                // System admin may pay if flat linked; no forced password change
            }
            else if (user.MustChangePassword)
            {
                return RedirectToAction("ChangePasswordRequired", "Account");
            }

            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            if (string.IsNullOrEmpty(flat))
            {
                if (User.IsInRole("Admin"))
                {
                    TempData["Error"] =
                        "Your admin account has no flat linked. You cannot use My Payments until a flat is set.";
                    return RedirectToAction("Index", "Home");
                }

                return RedirectToAction("CompleteProfile", "Account");
            }

            return null;
        }

        private async Task<ResidentPaymentsViewModel> BuildPaymentsViewModelAsync(
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
