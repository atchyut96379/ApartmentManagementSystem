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
            var result = await _notifications.SendPendingPaymentRemindersAsync();

            await _auditLog.LogAsync(
                AuditActions.PaymentReminderSent,
                $"Bulk reminders: {result.EmailSent} email, {result.SmsSent} SMS, {result.Skipped} skipped.");

            if (result.EmailSent == 0 && result.SmsSent == 0 && result.Errors.Count == 0)
            {
                TempData["Success"] =
                    $"Reminders processed. {result.Skipped} resident(s) skipped. Configure Integrations for live email/SMS.";
            }
            else
            {
                TempData["Success"] =
                    $"Reminders sent: {result.EmailSent} email(s), {result.SmsSent} SMS. Skipped: {result.Skipped}.";
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
            }
            else
            {
                await _auditLog.LogAsync(
                    AuditActions.PaymentReminderSent,
                    $"Reminder sent to flat {result.FlatNumber} ({result.ResidentName}).",
                    entityType: "Maintenance",
                    entityId: maintenanceId,
                    flatNumber: result.FlatNumber);

                TempData["Success"] =
                    $"Reminder sent to {result.ResidentName} (flat {result.FlatNumber}).";
            }

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
