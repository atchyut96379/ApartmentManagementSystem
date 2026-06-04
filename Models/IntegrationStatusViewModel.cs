namespace ApartmentManagementSystem.Models
{
    public class IntegrationStatusViewModel
    {
        public bool EmailReady { get; set; }

        public string EmailStatus { get; set; } = string.Empty;

        public bool SmsReady { get; set; }

        public string SmsStatus { get; set; } = string.Empty;

        public string SmsProvider { get; set; } = string.Empty;

        public bool RazorpayReady { get; set; }

        public string PaymentProvider { get; set; } = string.Empty;

        public string RazorpayStatus { get; set; } = string.Empty;

        public bool WebhookReady { get; set; }

        public string AppUrl { get; set; } = string.Empty;

        public string WebhookUrl { get; set; } = string.Empty;
    }
}
