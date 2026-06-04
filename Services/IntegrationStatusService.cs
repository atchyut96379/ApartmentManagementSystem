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
                SmsStatus = smsReady
                    ? $"{notification.SmsProvider} configured"
                    : "Fill MSG91 or Twilio fields below, check Enable SMS, and save.",
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
