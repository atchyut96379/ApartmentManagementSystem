namespace ApartmentManagementSystem.Models
{
    public class PaymentCheckoutViewModel
    {
        public int MaintenanceId { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string ResidentName { get; set; } = string.Empty;

        public string Month { get; set; } = string.Empty;

        public int Year { get; set; }

        public decimal Amount { get; set; }

        public decimal Fine { get; set; }

        public decimal TotalAmount { get; set; }

        public string ReturnUrl { get; set; } = string.Empty;

        public string Provider { get; set; } = "Simulation";

        public string? RazorpayKeyId { get; set; }

        public string? RazorpayOrderId { get; set; }

        public long AmountPaise { get; set; }

        public bool RazorpayReady { get; set; }

        public bool ShowSimulation { get; set; }

        public bool IsTestMode { get; set; }

        public string? RazorpayError { get; set; }

        public List<string> SetupSteps { get; set; } = new();
    }
}
