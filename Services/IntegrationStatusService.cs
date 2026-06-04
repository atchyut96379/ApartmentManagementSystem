using ApartmentManagementSystem.Models;

namespace ApartmentManagementSystem.Services
{
    public class IntegrationStatusService
    {
        private readonly IntegrationsSettingsStore _store;

        public IntegrationStatusService(IntegrationsSettingsStore store)
        {
            _store = store;
        }

        public IntegrationStatusViewModel GetStatus(string appUrl, string webhookPath)
        {
            var notification = _store.GetNotificationSettings();
            var payment = _store.GetPaymentSettings();

            var emailReady = notification.IsEmailConfigured;
            var smsReady = notification.IsSmsConfigured;
            var smsSimulation = notification.IsSimulationSms;
            var whatsAppReady = notification.EnableWhatsAppReminders &&
                                (notification.IsClickToChatWhatsApp ||
                                 notification.IsMsg91WhatsAppConfigured ||
                                 notification.IsSimulationWhatsApp);
            var razorpayReady = payment.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase) &&
                                payment.Razorpay.IsConfigured;

            return new IntegrationStatusViewModel
            {
                EmailReady = emailReady,
                EmailStatus = emailReady
                    ? $"SMTP {notification.SmtpHost}:{notification.SmtpPort} as {notification.FromEmail}"
                    : "Fill the form below, check Enable email, and save.",
                SmsReady = smsReady,
                SmsProvider = notification.SmsProvider,
                SmsStatus = smsSimulation
                    ? "Simulation — reminders log only (no real SMS). Add MSG91 keys for live SMS."
                    : notification.IsMsg91FlowConfigured
                        ? $"{notification.SmsProvider} + DLT Flow ID configured"
                        : smsReady
                            ? $"{notification.SmsProvider} configured — add Flow ID for India delivery"
                            : "Enable SMS, add MSG91 Auth Key + Sender ID, Save (or Azure app settings).",
                WhatsAppReady = whatsAppReady,
                WhatsAppApiReady = notification.IsMsg91WhatsAppConfigured,
                WhatsAppProvider = notification.WhatsAppProvider,
                WhatsAppStatus = notification.UseWhatsAppClickToChatForReminders
                    ? "Click-to-Chat — WhatsApp buttons on Collection dashboard (tap Send on association phone). MSG91 API paused until templates appear in Send WhatsApp."
                    : notification.IsSimulationWhatsApp
                        ? "WhatsApp simulation (no real message)."
                        : notification.IsMsg91WhatsAppConfigured
                            ? "MSG91 WhatsApp API enabled for automatic reminders."
                            : notification.EnableWhatsAppReminders
                                ? "Enable MSG91 WhatsApp API below after templates work in MSG91."
                                : "Enable WhatsApp reminders below.",
                PaymentProvider = payment.Provider,
                RazorpayReady = razorpayReady,
                RazorpayStatus = razorpayReady
                    ? "Live Razorpay checkout enabled"
                    : payment.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
                        ? "Add Razorpay Key ID and Secret below, then save"
                        : "Set Provider to Razorpay and add keys below",
                WebhookReady = payment.Razorpay.IsWebhookConfigured,
                AppUrl = appUrl,
                WebhookUrl = appUrl.TrimEnd('/') + webhookPath
            };
        }
    }
}
