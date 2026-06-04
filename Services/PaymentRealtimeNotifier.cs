using ApartmentManagementSystem.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ApartmentManagementSystem.Services
{
    public class PaymentRealtimeNotifier
    {
        private readonly IHubContext<PaymentUpdatesHub> _hub;

        public PaymentRealtimeNotifier(IHubContext<PaymentUpdatesHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyPaymentUpdatedAsync(
            int maintenanceId,
            string flatNumber,
            string month,
            int year,
            bool isPaid)
        {
            await _hub.Clients.All.SendAsync(
                "PaymentUpdated",
                new
                {
                    maintenanceId,
                    flatNumber,
                    month,
                    year,
                    isPaid,
                    at = DateTime.UtcNow
                });
        }
    }
}
