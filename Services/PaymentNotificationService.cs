using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services.Email;
using ApartmentManagementSystem.Services.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class PaymentNotificationResult
    {
        public int EmailSent { get; set; }

        public int SmsSent { get; set; }

        public int Skipped { get; set; }

        public List<string> Errors { get; set; } = new();
    }

    public class PaymentNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;
        private readonly MaintenanceFineService _fineService;
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly SocietySettings _society;
        private readonly SmtpEmailSender _emailSender;
        private readonly SmsSenderService _smsSender;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<PaymentNotificationService> _logger;

        public PaymentNotificationService(
            ApplicationDbContext context,
            MaintenanceBillingService billingService,
            MaintenanceFineService fineService,
            IntegrationsSettingsStore settingsStore,
            IOptions<SocietySettings> society,
            SmtpEmailSender emailSender,
            SmsSenderService smsSender,
            IHttpContextAccessor httpContextAccessor,
            ILogger<PaymentNotificationService> logger)
        {
            _context = context;
            _billingService = billingService;
            _fineService = fineService;
            _settingsStore = settingsStore;
            _society = society.Value;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<PaymentNotificationResult> SendPendingPaymentRemindersAsync()
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var result = new PaymentNotificationResult();
            var month = DateTime.Now.ToString("MMMM");
            var year = DateTime.Now.Year;
            var apartmentName = _society.ApartmentName ?? "your apartment";
            var appUrl = _settingsStore.GetApplicationSettings()
                .GetAppUrl(_httpContextAccessor.HttpContext?.Request);
            var payUrl = $"{appUrl}/ResidentPayments/MyPayments";

            var residents = await _context.Residents.ToListAsync();
            var maintenances = await _context.Maintenances
                .Where(m => m.Year == year && !m.PaymentStatus)
                .ToListAsync();

            foreach (var resident in residents)
            {
                if (resident.MemberType == ResidentMemberType.AssociationAdmin)
                {
                    continue;
                }

                var maintenance = maintenances.FirstOrDefault(m =>
                    FlatNumberHelper.Match(m.FlatNumber, resident.FlatNumber) &&
                    string.Equals(m.Month, month, StringComparison.OrdinalIgnoreCase));

                if (maintenance == null)
                {
                    result.Skipped++;
                    continue;
                }

                var fine = _fineService.CalculateFine(maintenance);
                var total = maintenance.Amount + fine;
                var dueText = maintenance.DueDate.ToString("dd-MM-yyyy");

                var subject = $"Pending maintenance — {month} {year} ({apartmentName})";
                var plain =
                    $"Dear {resident.OwnerName},\n\n" +
                    $"Maintenance for {month} {year} (Flat {resident.FlatNumber}) is pending.\n" +
                    $"Amount: ₹{maintenance.Amount:N2}" +
                    (fine > 0 ? $", Late fine: ₹{fine:N2}" : "") +
                    $", Total due: ₹{total:N2}\n" +
                    $"Due date: {dueText}\n\n" +
                    $"Pay online: {payUrl}\n\n" +
                    $"— {apartmentName} Management";

                var html =
                    $"<p>Dear <strong>{resident.OwnerName}</strong>,</p>" +
                    $"<p>Maintenance for <strong>{month} {year}</strong> (Flat <strong>{resident.FlatNumber}</strong>) is <span style='color:#c00'>pending</span>.</p>" +
                    $"<ul><li>Amount: ₹{maintenance.Amount:N2}</li>" +
                    (fine > 0 ? $"<li>Late fine: ₹{fine:N2}</li>" : "") +
                    $"<li><strong>Total due: ₹{total:N2}</strong></li>" +
                    $"<li>Due date: {dueText}</li></ul>" +
                    $"<p><a href='{payUrl}' style='background:#198754;color:#fff;padding:10px 16px;text-decoration:none;border-radius:4px'>Pay now</a></p>" +
                    $"<p>— {apartmentName} Management</p>";

                var sentAny = await SendToResidentAsync(
                    resident,
                    subject,
                    plain,
                    html,
                    $"{apartmentName}: Pending maintenance {month} {year}, flat {resident.FlatNumber}. Total ₹{total:N0}. Pay: {payUrl}",
                    result);
            }

            return result;
        }

        public async Task<SingleReminderResult> SendReminderForMaintenanceAsync(int maintenanceId)
        {
            var maintenance = await _context.Maintenances.FindAsync(maintenanceId);
            if (maintenance == null)
            {
                return new SingleReminderResult { Error = "Payment record not found." };
            }

            if (maintenance.PaymentStatus)
            {
                return new SingleReminderResult { Error = "This flat is already marked paid." };
            }

            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => FlatNumberHelper.Match(r.FlatNumber, maintenance.FlatNumber));

            if (resident == null)
            {
                return new SingleReminderResult { Error = "No resident found for this flat." };
            }

            var fine = _fineService.CalculateFine(maintenance);
            var total = maintenance.Amount + fine;
            var apartmentName = _society.ApartmentName ?? "your apartment";
            var appUrl = _settingsStore.GetApplicationSettings()
                .GetAppUrl(_httpContextAccessor.HttpContext?.Request);
            var payUrl = $"{appUrl}/ResidentPayments/MyPayments";
            var dueText = maintenance.DueDate.ToString("dd-MM-yyyy");

            var subject =
                $"Pending maintenance — {maintenance.Month} {maintenance.Year} ({apartmentName})";
            var plain =
                $"Dear {resident.OwnerName},\n\n" +
                $"Maintenance for {maintenance.Month} {maintenance.Year} (Flat {resident.FlatNumber}) is pending.\n" +
                $"Total due: ₹{total:N2}. Due: {dueText}\nPay: {payUrl}\n\n— {apartmentName}";
            var html =
                $"<p>Dear <strong>{resident.OwnerName}</strong>,</p>" +
                $"<p>Pending maintenance <strong>{maintenance.Month} {maintenance.Year}</strong> — total <strong>₹{total:N2}</strong>.</p>" +
                $"<p><a href='{payUrl}'>Pay now</a></p>";
            var sms =
                $"{apartmentName}: Pending maintenance {maintenance.Month} {maintenance.Year}, flat {resident.FlatNumber}. Due ₹{total:N0}. Pay: {payUrl}";

            var result = new PaymentNotificationResult();
            var sent = await SendToResidentAsync(resident, subject, plain, html, sms, result);

            if (!sent)
            {
                return new SingleReminderResult
                {
                    Error = result.Errors.FirstOrDefault() ??
                            "No email or SMS could be sent. Add phone/email on resident record or configure Integrations."
                };
            }

            return new SingleReminderResult
            {
                Sent = true,
                FlatNumber = resident.FlatNumber,
                ResidentName = resident.OwnerName
            };
        }

        public async Task SendPaymentConfirmationAsync(Resident resident, Maintenance maintenance)
        {
            if (!_settingsStore.GetNotificationSettings().SendConfirmationAfterPayment)
            {
                return;
            }

            var apartmentName = _society.ApartmentName ?? "Apartment Management";
            var appUrl = _settingsStore.GetApplicationSettings()
                .GetAppUrl(_httpContextAccessor.HttpContext?.Request);
            var total = maintenance.TotalPaidAmount;
            var subject = $"Payment received — {maintenance.Month} {maintenance.Year}";
            var plain =
                $"Dear {resident.OwnerName},\n\n" +
                $"Thank you. Payment received for {maintenance.Month} {maintenance.Year}.\n" +
                $"Flat: {maintenance.FlatNumber}\nTotal: ₹{total:N2}\n" +
                $"Transaction: {maintenance.TransactionId}\nReceipt: {maintenance.ReceiptNumber}\n\n" +
                $"{appUrl}/ResidentPayments/MyPayments\n\n— {apartmentName}";

            var html =
                $"<p>Dear <strong>{resident.OwnerName}</strong>,</p>" +
                $"<p>Thank you — we received your payment for <strong>{maintenance.Month} {maintenance.Year}</strong>.</p>" +
                $"<ul><li>Flat: {maintenance.FlatNumber}</li><li>Total: ₹{total:N2}</li>" +
                $"<li>Transaction: {maintenance.TransactionId}</li><li>Receipt: {maintenance.ReceiptNumber}</li></ul>" +
                $"<p><a href='{appUrl}/ResidentPayments/MyPayments'>View receipt</a></p>";

            var sms =
                $"{apartmentName}: Payment ₹{total:N0} received for {maintenance.Month} {maintenance.Year}. Txn {maintenance.TransactionId}.";

            var dummy = new PaymentNotificationResult();
            await SendToResidentAsync(resident, subject, plain, html, sms, dummy);
        }

        public async Task<(bool Success, string? Error)> SendTestEmailAsync(string to)
        {
            var apartmentName = _society.ApartmentName ?? "Apartment Management";
            return await _emailSender.SendAsync(
                to,
                $"Test email — {apartmentName}",
                "This is a test email from your apartment management portal. If you received this, SMTP is configured correctly.",
                "<p>This is a <strong>test email</strong> from your apartment management portal.</p><p>SMTP is working.</p>");
        }

        public async Task<(bool Success, string? Error)> SendTestSmsAsync(string phone)
        {
            var apartmentName = _society.ApartmentName ?? "Apartment Management";
            return await _smsSender.SendAsync(
                phone,
                $"{apartmentName}: Test SMS from apartment portal. SMS integration is working.");
        }

        private async Task<bool> SendToResidentAsync(
            Resident resident,
            string subject,
            string plain,
            string html,
            string smsBody,
            PaymentNotificationResult result)
        {
            var sentAny = false;

            if (!string.IsNullOrWhiteSpace(resident.Email))
            {
                var (ok, err) = await _emailSender.SendAsync(resident.Email.Trim(), subject, plain, html);
                if (ok)
                {
                    result.EmailSent++;
                    sentAny = true;
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    result.Errors.Add($"Email {resident.Email}: {err}");
                }
            }

            if (!string.IsNullOrWhiteSpace(resident.PhoneNumber))
            {
                var (ok, err) = await _smsSender.SendAsync(resident.PhoneNumber.Trim(), smsBody);
                if (ok)
                {
                    result.SmsSent++;
                    sentAny = true;
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    result.Errors.Add($"SMS {resident.PhoneNumber}: {err}");
                }
            }

            if (!sentAny)
            {
                result.Skipped++;
            }

            return sentAny;
        }
    }
}
