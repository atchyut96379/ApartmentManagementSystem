using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class MaintenanceFineService
    {
        private readonly PaymentGatewaySettings _settings;

        public MaintenanceFineService(IOptions<PaymentGatewaySettings> settings)
        {
            _settings = settings.Value;
        }

        public decimal CalculateFine(Maintenance maintenance)
        {
            if (maintenance.PaymentStatus)
            {
                return maintenance.FineAmount;
            }

            if (DateTime.Today <= maintenance.DueDate.Date)
            {
                return 0m;
            }

            return _settings.LateFineAmount;
        }
    }
}
