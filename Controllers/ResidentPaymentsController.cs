using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Resident")]
    public class ResidentPaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ResidentProfileService _residentProfileService;
        private readonly MaintenanceBillingService _billingService;

        public ResidentPaymentsController(
            ApplicationDbContext context,
            ResidentProfileService residentProfileService,
            MaintenanceBillingService billingService)
        {
            _context = context;
            _residentProfileService = residentProfileService;
            _billingService = billingService;
        }

        public async Task<IActionResult> MyPayments()
        {
            return View(await BuildPaymentsViewModelAsync(
                pendingOnly: false,
                showReceiptColumns: true,
                returnUrl: "/ResidentPayments/MyPayments"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayNow(int id, string? returnUrl)
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var flat = await _residentProfileService.GetFlatForUserAsync(User);
            if (string.IsNullOrEmpty(flat))
            {
                TempData["Error"] = "Please complete your resident profile with your flat number first.";
                return RedirectToAction("CompleteProfile", "Account");
            }

            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }

            if (!FlatNumberHelper.Match(maintenance.FlatNumber, flat))
            {
                TempData["Error"] = "You can only pay maintenance for your own flat.";
                return RedirectToAction("Index", "Home");
            }

            if (maintenance.PaymentStatus)
            {
                TempData["Success"] = "This payment is already marked as paid.";
                return RedirectToLocal(returnUrl, "/ResidentPayments/MyPayments");
            }

            maintenance.PaymentStatus = true;
            maintenance.PaymentDate = DateTime.Now;
            maintenance.PaidDate = DateTime.Now;
            maintenance.ReceiptNumber = "RCPT-" + DateTime.Now.Ticks;
            maintenance.Remarks = "Paid online by resident";

            _context.Update(maintenance);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Payment of ₹{maintenance.Amount:N2} for {maintenance.Month} {maintenance.Year} completed successfully.";

            return RedirectToLocal(returnUrl, "/ResidentPayments/MyPayments");
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

        private IActionResult RedirectToLocal(string? returnUrl, string fallback)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect(fallback);
        }
    }
}
