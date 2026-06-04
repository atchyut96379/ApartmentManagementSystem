namespace ApartmentManagementSystem.Models
{
    public class IntegrationsLocalFile
    {
        public NotificationSettings? Notification { get; set; }

        public PaymentGatewaySettings? Payment { get; set; }

        public ApplicationSettings? Application { get; set; }
    }
}
