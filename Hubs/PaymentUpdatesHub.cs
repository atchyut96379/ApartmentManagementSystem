using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApartmentManagementSystem.Hubs
{
    [Authorize(Roles = "Admin,Resident")]
    public class PaymentUpdatesHub : Hub
    {
    }
}
