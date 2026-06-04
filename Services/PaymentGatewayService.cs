using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class PaymentCompletionResult
    {
        public bool Succeeded { get; set; }

        public int MaintenanceId { get; set; }

        public string? Error { get; set; }
    }

    public class PaymentGatewayService
    {
        private readonly ApplicationDbContext _context;
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly MaintenanceFineService _fineService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PaymentNotificationService _notifications;
        private readonly PaymentRealtimeNotifier _realtime;
        private readonly AuditLogService _auditLog;

        public PaymentGatewayService(
            ApplicationDbContext context,
            IntegrationsSettingsStore settingsStore,
            MaintenanceFineService fineService,
            IHttpClientFactory httpClientFactory,
            PaymentNotificationService notifications,
            PaymentRealtimeNotifier realtime,
            AuditLogService auditLog)
        {
            _context = context;
            _settingsStore = settingsStore;
            _fineService = fineService;
            _httpClientFactory = httpClientFactory;
            _notifications = notifications;
            _realtime = realtime;
            _auditLog = auditLog;
        }

        public async Task<Maintenance?> GetMaintenanceAsync(int maintenanceId) =>
            await _context.Maintenances.FindAsync(maintenanceId);

        public async Task<PaymentCompletionResult> MarkPaidOfflineAsync(
            int maintenanceId,
            string payerName)
        {
            return await RecordSuccessfulPaymentAsync(
                maintenanceId,
                payerName,
                "Offline",
                "TXN-CASH-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                null);
        }

        private PaymentGatewaySettings PaymentSettings => _settingsStore.GetPaymentSettings();

        public async Task<PaymentCheckoutViewModel?> BuildCheckoutAsync(
            Maintenance maintenance,
            string residentName,
            string returnUrl)
        {
            if (maintenance.PaymentStatus)
            {
                return null;
            }

            var fine = _fineService.CalculateFine(maintenance);
            var total = maintenance.Amount + fine;
            var useRazorpay = PaymentSettings.Provider.Equals(
                "Razorpay",
                StringComparison.OrdinalIgnoreCase);
            var razorpayConfigured = PaymentSettings.Razorpay.IsConfigured;

            var model = new PaymentCheckoutViewModel
            {
                MaintenanceId = maintenance.Id,
                FlatNumber = maintenance.FlatNumber,
                ResidentName = residentName,
                Month = maintenance.Month,
                Year = maintenance.Year,
                Amount = maintenance.Amount,
                Fine = fine,
                TotalAmount = total,
                ReturnUrl = returnUrl,
                Provider = useRazorpay && razorpayConfigured ? "Razorpay" : "Simulation",
                AmountPaise = (long)Math.Round(total * 100, MidpointRounding.AwayFromZero),
                ShowSimulation = !useRazorpay || !razorpayConfigured,
                IsTestMode = PaymentSettings.Razorpay.KeyId.StartsWith(
                    "rzp_test_",
                    StringComparison.OrdinalIgnoreCase)
            };

            if (useRazorpay && razorpayConfigured)
            {
                model.RazorpayReady = true;
                model.RazorpayKeyId = PaymentSettings.Razorpay.KeyId;
                try
                {
                    model.RazorpayOrderId = await CreateRazorpayOrderAsync(
                        maintenance,
                        model.AmountPaise);
                }
                catch (Exception ex)
                {
                    model.RazorpayReady = false;
                    model.RazorpayError = ex.Message;
                    model.ShowSimulation = true;
                }
            }
            else if (useRazorpay)
            {
                model.SetupSteps = BuildRazorpaySetupSteps(razorpayConfigured);
            }

            return model;
        }

        public bool IsLiveRazorpayEnabled()
        {
            return PaymentSettings.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase) &&
                   PaymentSettings.Razorpay.IsConfigured;
        }

        private static List<string> BuildRazorpaySetupSteps(bool keysConfigured)
        {
            var steps = new List<string>
            {
                "Log in as system Admin or committee member with admin access.",
                "Open Integrations in the left menu (or go to /Integrations).",
                "Set Provider to Razorpay (live).",
                "Paste Razorpay Key ID and Key Secret from dashboard.razorpay.com → Account & Settings → API Keys.",
                "Save settings, then return here and refresh this page."
            };

            if (!keysConfigured)
            {
                steps.Insert(0, "Razorpay keys are missing — add them in Integrations and click Save.");
            }

            return steps;
        }

        public async Task<PaymentCompletionResult> CompleteSimulatedPaymentAsync(
            int maintenanceId,
            string payerName)
        {
            return await RecordSuccessfulPaymentAsync(
                maintenanceId,
                payerName,
                "Simulation",
                "TXN-SIM-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                null);
        }

        public async Task<PaymentCompletionResult> CompleteRazorpayPaymentAsync(
            int maintenanceId,
            string payerName,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature)
        {
            if (!PaymentSettings.Razorpay.IsConfigured)
            {
                return new PaymentCompletionResult
                {
                    Error = "Razorpay is not configured. Add KeyId and KeySecret under Payment:Razorpay in appsettings."
                };
            }

            if (!VerifyRazorpaySignature(razorpayOrderId, razorpayPaymentId, razorpaySignature))
            {
                return new PaymentCompletionResult
                {
                    Error = "Payment verification failed."
                };
            }

            var maintenance = await _context.Maintenances.FindAsync(maintenanceId);
            if (maintenance == null)
            {
                return new PaymentCompletionResult { Error = "Payment record not found." };
            }

            if (!string.IsNullOrEmpty(maintenance.RazorpayOrderId) &&
                !string.Equals(
                    maintenance.RazorpayOrderId,
                    razorpayOrderId,
                    StringComparison.Ordinal))
            {
                return new PaymentCompletionResult
                {
                    Error = "Order id does not match this payment."
                };
            }

            return await RecordSuccessfulPaymentAsync(
                maintenanceId,
                payerName,
                "Razorpay",
                razorpayPaymentId,
                razorpayOrderId);
        }

        public async Task<bool> ProcessWebhookAsync(string rawBody, string? signatureHeader)
        {
            if (!PaymentSettings.Razorpay.IsWebhookConfigured)
            {
                return false;
            }

            if (string.IsNullOrEmpty(signatureHeader) ||
                !VerifyWebhookSignature(rawBody, signatureHeader))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (!root.TryGetProperty("event", out var eventProp))
            {
                return false;
            }

            var eventName = eventProp.GetString();
            if (!string.Equals(eventName, "payment.captured", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("payment", out var paymentWrapper) ||
                !paymentWrapper.TryGetProperty("entity", out var payment))
            {
                return false;
            }

            var paymentId = payment.GetProperty("id").GetString();
            var orderId = payment.GetProperty("order_id").GetString();
            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(orderId))
            {
                return false;
            }

            var maintenance = await _context.Maintenances
                .FirstOrDefaultAsync(m => m.RazorpayOrderId == orderId);

            if (maintenance == null)
            {
                return false;
            }

            if (maintenance.PaymentStatus &&
                string.Equals(maintenance.TransactionId, paymentId, StringComparison.Ordinal))
            {
                return true;
            }

            var payerName = maintenance.PayerName ?? "Resident";
            if (payment.TryGetProperty("notes", out var notes) &&
                notes.TryGetProperty("payer_name", out var payerNote))
            {
                payerName = payerNote.GetString() ?? payerName;
            }

            var result = await RecordSuccessfulPaymentAsync(
                maintenance.Id,
                payerName,
                "Razorpay",
                paymentId,
                orderId);

            return result.Succeeded;
        }

        public PaymentReceiptViewModel? BuildReceipt(Maintenance maintenance)
        {
            if (!maintenance.PaymentStatus ||
                string.IsNullOrEmpty(maintenance.TransactionId))
            {
                return null;
            }

            return new PaymentReceiptViewModel
            {
                MaintenanceId = maintenance.Id,
                ReceiptNumber = maintenance.ReceiptNumber ?? "-",
                FlatNumber = maintenance.FlatNumber,
                Name = maintenance.PayerName ?? "-",
                Month = maintenance.Month,
                Year = maintenance.Year,
                Amount = maintenance.Amount,
                Fine = maintenance.FineAmount,
                TotalPaid = maintenance.TotalPaidAmount,
                TransactionId = maintenance.TransactionId,
                PaymentGateway = maintenance.PaymentGateway ?? "-",
                PaidAt = maintenance.PaidDate ?? maintenance.PaymentDate ?? DateTime.Now
            };
        }

        private async Task<PaymentCompletionResult> RecordSuccessfulPaymentAsync(
            int maintenanceId,
            string payerName,
            string gateway,
            string transactionId,
            string? gatewayOrderId)
        {
            var maintenance = await _context.Maintenances.FindAsync(maintenanceId);
            if (maintenance == null)
            {
                return new PaymentCompletionResult { Error = "Payment record not found." };
            }

            if (maintenance.PaymentStatus)
            {
                return new PaymentCompletionResult
                {
                    Succeeded = true,
                    MaintenanceId = maintenanceId
                };
            }

            var fine = _fineService.CalculateFine(maintenance);
            var total = maintenance.Amount + fine;
            var now = DateTime.Now;

            maintenance.PaymentStatus = true;
            maintenance.PaymentDate = now;
            maintenance.PaidDate = now;
            maintenance.FineAmount = fine;
            maintenance.TotalPaidAmount = total;
            maintenance.TransactionId = transactionId;
            maintenance.PayerName = payerName.Trim();
            maintenance.PaymentGateway = gateway;
            maintenance.ReceiptNumber = "RCPT-" + now.ToString("yyyyMMddHHmmss");
            maintenance.RazorpayOrderId = gatewayOrderId ?? maintenance.RazorpayOrderId;
            maintenance.Remarks = string.IsNullOrEmpty(gatewayOrderId)
                ? $"Paid via {gateway}"
                : $"Paid via {gateway} (order {gatewayOrderId})";

            await _context.SaveChangesAsync();

            var residents = await _context.Residents.ToListAsync();
            var resident = FlatNumberHelper.FindResidentForFlat(
                residents,
                maintenance.FlatNumber);
            if (resident != null)
            {
                await _notifications.SendPaymentConfirmationAsync(resident, maintenance);
            }

            await _realtime.NotifyPaymentUpdatedAsync(
                maintenance.Id,
                maintenance.FlatNumber,
                maintenance.Month,
                maintenance.Year,
                true);

            await _auditLog.LogAsync(
                AuditActions.PaymentRecorded,
                $"Payment via {gateway}. Payer: {payerName.Trim()}. Total ₹{maintenance.TotalPaidAmount:N2}. Txn {transactionId}.",
                entityType: "Maintenance",
                entityId: maintenanceId,
                flatNumber: maintenance.FlatNumber);

            return new PaymentCompletionResult
            {
                Succeeded = true,
                MaintenanceId = maintenanceId
            };
        }

        private async Task<string> CreateRazorpayOrderAsync(
            Maintenance maintenance,
            long amountPaise)
        {
            var client = _httpClientFactory.CreateClient();
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{PaymentSettings.Razorpay.KeyId}:{PaymentSettings.Razorpay.KeySecret}"));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var payload = new
            {
                amount = amountPaise,
                currency = "INR",
                receipt = $"maint_{maintenance.Id}_{DateTime.UtcNow.Ticks}",
                payment_capture = true,
                notes = new Dictionary<string, string>
                {
                    ["maintenance_id"] = maintenance.Id.ToString(),
                    ["flat_number"] = maintenance.FlatNumber
                }
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                "https://api.razorpay.com/v1/orders",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    "Could not create Razorpay order. Check API keys. " + errorBody);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var orderId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Razorpay order id missing.");

            maintenance.RazorpayOrderId = orderId;
            await _context.SaveChangesAsync();

            return orderId;
        }

        private bool VerifyRazorpaySignature(
            string orderId,
            string paymentId,
            string signature)
        {
            var expected = ComputeHmacHex($"{orderId}|{paymentId}", PaymentSettings.Razorpay.KeySecret);
            return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
        }

        private bool VerifyWebhookSignature(string rawBody, string signature)
        {
            var expected = ComputeHmacHex(rawBody, PaymentSettings.Razorpay.WebhookSecret);
            return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeHmacHex(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
