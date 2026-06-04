namespace ApartmentManagementSystem.Models
{
    public class PaymentGatewaySettings
    {
        public const string SectionName = "Payment";

        /// <summary>Simulation or Razorpay</summary>
        public string Provider { get; set; } = "Simulation";

        public decimal LateFineAmount { get; set; } = 50m;

        public RazorpaySettings Razorpay { get; set; } = new();
    }

    public class RazorpaySettings
    {
        public string KeyId { get; set; } = string.Empty;

        public string KeySecret { get; set; } = string.Empty;

        /// <summary>Webhook secret from Razorpay Dashboard → Webhooks (for payment.captured).</summary>
        public string WebhookSecret { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(KeyId) &&
            !string.IsNullOrWhiteSpace(KeySecret);

        public bool IsWebhookConfigured =>
            IsConfigured && !string.IsNullOrWhiteSpace(WebhookSecret);
    }
}
