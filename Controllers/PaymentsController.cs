using ApartmentManagementSystem.Filters;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Resident")]
    public class PaymentsController : Controller
    {
        private readonly MonthlyPaymentDetailsService _paymentDetails;
        private readonly PaymentNotificationService _notifications;
        private readonly CommitteeAccessService _committeeAccess;
        private readonly PaymentGatewayService _paymentGateway;
        private readonly AuditLogService _auditLog;

        public PaymentsController(
            MonthlyPaymentDetailsService paymentDetails,
            PaymentNotificationService notifications,
            CommitteeAccessService committeeAccess,
            PaymentGatewayService paymentGateway,
            AuditLogService auditLog)
        {
            _paymentDetails = paymentDetails;
            _notifications = notifications;
            _committeeAccess = committeeAccess;
            _paymentGateway = paymentGateway;
            _auditLog = auditLog;
        }

        public async Task<IActionResult> Index(int? year, string? month)
        {
            var isCommittee = await _committeeAccess.IsCommitteeAdminAsync(User);
            var user = await _committeeAccess.GetUserAsync(User);
            var isSystemAdmin = _committeeAccess.IsSystemAdmin(user);

            if (!isCommittee && !isSystemAdmin)
            {
                return RedirectToAction("MyPayments", "ResidentPayments");
            }

            var model = await _paymentDetails.BuildForPeriodAsync(year, month);
            model.CanSendReminders = isCommittee;
            model.CanManagePayments = isCommittee;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> SendReminders(int? year, string? month)
        {
            var result = await _notifications.SendPendingPaymentRemindersAsync(year, month);

            await _auditLog.LogAsync(
                AuditActions.PaymentReminderSent,
                $"Bulk SMS reminders ({month} {year}): {result.SmsSent} sent, {result.Skipped} skipped.");

            var sentTotal = result.SmsSent + result.WhatsAppSent;
            if (sentTotal == 0 && result.Errors.Count == 0)
            {
                TempData["Success"] =
                    "No pending flats for this month, or all skipped. Check billing period and resident mobile numbers.";
            }
            else if (sentTotal > 0)
            {
                var channel = result.WhatsAppSent > 0 ? "WhatsApp" : "SMS";
                TempData["Success"] =
                    $"{channel} reminders sent: {sentTotal}. Skipped: {result.Skipped}.";
            }
            else
            {
                TempData["Success"] =
                    $"No reminders sent. Skipped: {result.Skipped}. See Integrations or use per-flat WhatsApp links.";
            }

            if (result.Errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Take(3));
            }

            return RedirectToAction(nameof(Index), new { year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> RemindFlat(int maintenanceId, int? year, string? month)
        {
            var result = await _notifications.SendReminderForMaintenanceAsync(maintenanceId);

            if (!result.Sent)
            {
                TempData["Error"] = result.Error ?? "Could not send reminder.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            if (result.UseWhatsAppRedirect && !string.IsNullOrWhiteSpace(result.WhatsAppUrl))
            {
                await _auditLog.LogAsync(
                    AuditActions.PaymentReminderSent,
                    $"WhatsApp reminder opened for flat {result.FlatNumber} ({result.ResidentName}).",
                    entityType: "Maintenance",
                    entityId: maintenanceId,
                    flatNumber: result.FlatNumber);

                return Redirect(result.WhatsAppUrl);
            }

            await _auditLog.LogAsync(
                AuditActions.PaymentReminderSent,
                $"{result.Channel ?? "Reminder"} sent to flat {result.FlatNumber} ({result.ResidentName}).",
                entityType: "Maintenance",
                entityId: maintenanceId,
                flatNumber: result.FlatNumber);

            TempData["Success"] =
                $"{result.Channel ?? "Reminder"} sent to {result.ResidentName} (flat {result.FlatNumber}).";

            return RedirectToAction(nameof(Index), new { year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> MarkPaidOffline(
            int maintenanceId,
            string payerName,
            int? year,
            string? month)
        {
            if (string.IsNullOrWhiteSpace(payerName))
            {
                TempData["Error"] = "Enter who paid (name on receipt / UPI).";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            var result = await _paymentGateway.MarkPaidOfflineAsync(maintenanceId, payerName.Trim());

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error ?? "Could not record payment.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            var maintenance = await _paymentGateway.GetMaintenanceAsync(maintenanceId);
            if (maintenance != null)
            {
                await _auditLog.LogAsync(
                    AuditActions.OfflinePaymentMarked,
                    $"Offline/cash payment recorded. Payer: {payerName.Trim()}. Total ₹{maintenance.TotalPaidAmount:N2}.",
                    entityType: "Maintenance",
                    entityId: maintenanceId,
                    flatNumber: maintenance.FlatNumber);
            }

            TempData["Success"] = "Payment marked as paid (offline/cash).";
            return RedirectToAction(nameof(Index), new { year, month });
        }
    }
}
