namespace ApartmentManagementSystem.Models
{
    public class PaymentReceiptViewModel
    {
        public int MaintenanceId { get; set; }

        public string ReceiptNumber { get; set; } = string.Empty;

        public string FlatNumber { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Month { get; set; } = string.Empty;

        public int Year { get; set; }

        public decimal Amount { get; set; }

        public decimal Fine { get; set; }

        public decimal TotalPaid { get; set; }

        public string TransactionId { get; set; } = string.Empty;

        public string PaymentGateway { get; set; } = string.Empty;

        public DateTime PaidAt { get; set; }
    }
}
