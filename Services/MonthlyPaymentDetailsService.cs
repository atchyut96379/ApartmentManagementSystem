using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class MonthlyPaymentDetailsService
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;
        private readonly MaintenanceFineService _fineService;
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly PaymentReminderMessageBuilder _reminderMessages;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MonthlyPaymentDetailsService(
            ApplicationDbContext context,
            MaintenanceBillingService billingService,
            MaintenanceFineService fineService,
            IntegrationsSettingsStore settingsStore,
            PaymentReminderMessageBuilder reminderMessages,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _billingService = billingService;
            _fineService = fineService;
            _settingsStore = settingsStore;
            _reminderMessages = reminderMessages;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<MonthlyPaymentDetailsViewModel> BuildCurrentMonthAsync() =>
            BuildForPeriodAsync(null, null);

        public async Task<MonthlyPaymentDetailsViewModel> BuildForPeriodAsync(int? year, string? month)
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            var (resolvedYear, resolvedMonth) = BillingMonthHelper.ResolvePeriod(year, month);
            var yearValue = resolvedYear;
            var monthValue = resolvedMonth;

            var residents = await _context.Residents
                .OrderBy(r => r.FlatNumber)
                .ToListAsync();

            var maintenances = await _context.Maintenances
                .Where(m => m.Year == yearValue)
                .ToListAsync();

            var rows = new List<MonthlyPaymentRowViewModel>();
            var notification = _settingsStore.GetNotificationSettings();
            var useWhatsAppClick = notification.UseWhatsAppClickToChatForReminders;
            var payUrl = _reminderMessages.BuildPayUrl(
                _settingsStore,
                _httpContextAccessor.HttpContext?.Request);

            foreach (var resident in residents)
            {
                var flat = resident.FlatNumber.Trim();
                var maintenance = maintenances.FirstOrDefault(m =>
                    FlatNumberHelper.Match(m.FlatNumber, flat) &&
                    string.Equals(m.Month, monthValue, StringComparison.OrdinalIgnoreCase));

                var fine = maintenance != null
                    ? _fineService.CalculateFine(maintenance)
                    : 0m;
                var amount = maintenance?.Amount ?? _billingService.DefaultMonthlyAmount;
                var totalDue = amount + fine;

                var memberLabel = resident.MemberType == ResidentMemberType.AssociationAdmin
                    ? $"Committee — {resident.AssociationDesignation ?? "Admin"}"
                    : resident.MemberType == ResidentMemberType.Tenant
                        ? "Tenant"
                        : "Owner";

                var isPaid = maintenance?.PaymentStatus == true;
                string? whatsAppUrl = null;
                if (!isPaid && useWhatsAppClick && maintenance != null)
                {
                    var text = _reminderMessages.BuildReminderText(
                        resident.OwnerName,
                        maintenance.Month,
                        payUrl);
                    whatsAppUrl = _reminderMessages.BuildClickToChatUrl(resident.PhoneNumber, text);
                }

                rows.Add(new MonthlyPaymentRowViewModel
                {
                    MaintenanceId = maintenance?.Id,
                    ResidentId = resident.Id,
                    FlatNumber = flat,
                    ResidentName = resident.OwnerName,
                    ResidentPhone = resident.PhoneNumber,
                    WhatsAppReminderUrl = whatsAppUrl,
                    MemberLabel = memberLabel,
                    Amount = amount,
                    Fine = maintenance?.PaymentStatus == true
                        ? maintenance.FineAmount
                        : fine,
                    TotalDue = maintenance?.PaymentStatus == true
                        ? maintenance.TotalPaidAmount
                        : totalDue,
                    IsPaid = isPaid,
                    TransactionId = maintenance?.TransactionId,
                    PayerName = maintenance?.PayerName,
                    PaymentGateway = maintenance?.PaymentGateway,
                    ReceiptNumber = maintenance?.ReceiptNumber,
                    PaidAt = maintenance?.PaidDate ?? maintenance?.PaymentDate
                });
            }

            var pendingTotal = rows.Where(r => !r.IsPaid).Sum(r => r.TotalDue);

            return new MonthlyPaymentDetailsViewModel
            {
                Month = monthValue,
                Year = yearValue,
                PaidCount = rows.Count(r => r.IsPaid),
                PendingCount = rows.Count(r => !r.IsPaid),
                TotalCollected = rows.Where(r => r.IsPaid).Sum(r => r.TotalDue),
                TotalPending = pendingTotal,
                CollectionPercent = rows.Count == 0
                    ? 0
                    : Math.Round((decimal)rows.Count(r => r.IsPaid) / rows.Count * 100, 1),
                AvailablePeriods = BillingMonthHelper.GetRecentBillingPeriods(),
                Rows = rows,
                UseWhatsAppClickToChat = useWhatsAppClick
            };
        }
    }
}
