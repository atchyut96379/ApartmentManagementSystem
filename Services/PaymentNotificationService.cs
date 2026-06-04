using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services.Email;
using ApartmentManagementSystem.Services.Sms;
using ApartmentManagementSystem.Services.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class PaymentNotificationResult
    {
        public int EmailSent { get; set; }

        public int SmsSent { get; set; }

        public int WhatsAppSent { get; set; }

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
        private readonly WhatsAppSenderService _whatsAppSender;
        private readonly PaymentReminderMessageBuilder _reminderMessages;
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
            WhatsAppSenderService whatsAppSender,
            PaymentReminderMessageBuilder reminderMessages,
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
            _whatsAppSender = whatsAppSender;
            _reminderMessages = reminderMessages;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Task<PaymentNotificationResult> SendPendingPaymentRemindersAsync() =>
            SendPendingPaymentRemindersAsync(null, null);

        public async Task<PaymentNotificationResult> SendPendingPaymentRemindersAsync(
            int? year,
            string? month)
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var result = new PaymentNotificationResult();
            var (resolvedYear, resolvedMonth) = BillingMonthHelper.ResolvePeriod(year, month);
            var payUrl = BuildPayUrl();
            var notification = _settingsStore.GetNotificationSettings();
            if (notification.UseWhatsAppClickToChatForReminders)
            {
                result.Errors.Add(
                    "Use the green WhatsApp link on each pending flat (association phone → tap Send). MSG91 API is off until templates work in MSG91.");
                return result;
            }

            var residents = await _context.Residents.ToListAsync();
            var maintenances = (await _context.Maintenances
                .Where(m => m.Year == resolvedYear && !m.PaymentStatus)
                .ToListAsync())
                .Where(m => string.Equals(m.Month, resolvedMonth, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var resident in residents)
            {
                if (resident.MemberType == ResidentMemberType.AssociationAdmin)
                {
                    continue;
                }

                var maintenance = maintenances.FirstOrDefault(m =>
                    FlatNumberHelper.Match(m.FlatNumber, resident.FlatNumber));

                if (maintenance == null)
                {
                    continue;
                }

                var sms = BuildReminderSmsMessage(
                    resident.OwnerName,
                    maintenance.Month,
                    payUrl);

                await SendPaymentReminderAsync(resident, maintenance.Month, payUrl, sms, result);
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

            var residents = await _context.Residents.ToListAsync();
            var resident = FlatNumberHelper.FindResidentForFlat(
                residents,
                maintenance.FlatNumber);

            if (resident == null)
            {
                return new SingleReminderResult { Error = "No resident found for this flat." };
            }

            var payUrl = BuildPayUrl();
            var sms = BuildReminderSmsMessage(
                resident.OwnerName,
                maintenance.Month,
                payUrl);

            var notification = _settingsStore.GetNotificationSettings();
            if (notification.UseWhatsAppClickToChatForReminders)
            {
                var text = _reminderMessages.BuildReminderText(
                    resident.OwnerName,
                    maintenance.Month,
                    payUrl);
                var waUrl = _reminderMessages.BuildClickToChatUrl(resident.PhoneNumber, text);
                if (waUrl == null)
                {
                    return new SingleReminderResult
                    {
                        Error = "Add login mobile on Residents for this flat, then use WhatsApp."
                    };
                }

                return new SingleReminderResult
                {
                    Sent = true,
                    UseWhatsAppRedirect = true,
                    WhatsAppUrl = waUrl,
                    FlatNumber = resident.FlatNumber,
                    ResidentName = resident.OwnerName
                };
            }

            var result = new PaymentNotificationResult();
            var sent = await SendPaymentReminderAsync(
                resident,
                maintenance.Month,
                payUrl,
                sms,
                result);

            if (!sent)
            {
                return new SingleReminderResult
                {
                    Error = result.Errors.FirstOrDefault() ??
                            "Reminder could not be sent. Add login mobile and check Integrations."
                };
            }

            var channel = result.WhatsAppSent > 0 ? "WhatsApp" : "SMS";
            return new SingleReminderResult
            {
                Sent = true,
                Channel = channel,
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

        public async Task<(bool Success, string? Error)> SendTestWhatsAppAsync(string phone)
        {
            var settings = _settingsStore.GetNotificationSettings();
            if (settings.UseWhatsAppClickToChatForReminders)
            {
                return (false,
                    "MSG91 WhatsApp API is off. Use WhatsApp links on the collection dashboard, or enable API in Integrations after MSG91 shows templates.");
            }

            var payUrl = BuildPayUrl();
            var vars = _reminderMessages.BuildTemplateVariables("Test Resident", "June 2026", payUrl);
            var body = _reminderMessages.BuildReminderSmsBody("Test Resident", "June 2026", payUrl);

            return await _whatsAppSender.SendReminderAsync(phone.Trim(), vars, body);
        }

        private string BuildPayUrl() =>
            _reminderMessages.BuildPayUrl(
                _settingsStore,
                _httpContextAccessor.HttpContext?.Request);

        private string BuildReminderSmsMessage(
            string residentName,
            string month,
            string payUrl) =>
            _reminderMessages.BuildReminderSmsBody(residentName, month, payUrl);

        private static string GetFirstName(string residentName)
        {
            var firstName = residentName.Trim();
            var space = firstName.IndexOf(' ');
            if (space > 0)
            {
                firstName = firstName[..space];
            }

            return firstName;
        }

        private async Task<bool> SendPaymentReminderAsync(
            Resident resident,
            string month,
            string payUrl,
            string smsBody,
            PaymentNotificationResult result)
        {
            if (string.IsNullOrWhiteSpace(resident.PhoneNumber))
            {
                result.Errors.Add(
                    $"Flat {resident.FlatNumber}: Add login mobile on Residents list for reminders.");
                result.Skipped++;
                return false;
            }

            var settings = _settingsStore.GetNotificationSettings();
            var templateVars = _reminderMessages.BuildTemplateVariables(
                resident.OwnerName,
                month,
                payUrl);

            if (settings.EnableWhatsAppReminders &&
                (settings.IsMsg91WhatsAppConfigured || settings.IsSimulationWhatsApp))
            {
                var (waOk, waErr) = await _whatsAppSender.SendReminderAsync(
                    resident.PhoneNumber.Trim(),
                    templateVars,
                    smsBody);
                if (waOk)
                {
                    result.WhatsAppSent++;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(waErr))
                {
                    result.Errors.Add($"Flat {resident.FlatNumber}: {waErr}");
                }
            }

            if (!settings.EnableSms)
            {
                result.Skipped++;
                return false;
            }

            var flowVars = new Dictionary<string, string>
            {
                ["name"] = templateVars["name"],
                ["month"] = templateVars["month"],
                ["link"] = templateVars["link"]
            };

            var (ok, err) = await _smsSender.SendAsync(
                resident.PhoneNumber.Trim(),
                smsBody,
                flowVars);
            if (ok)
            {
                result.SmsSent++;
                return true;
            }

            result.Errors.Add(
                string.IsNullOrWhiteSpace(err)
                    ? $"Flat {resident.FlatNumber}: SMS failed."
                    : $"Flat {resident.FlatNumber}: {err}");
            result.Skipped++;
            return false;
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
