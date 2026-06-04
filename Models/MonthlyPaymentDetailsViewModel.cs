namespace ApartmentManagementSystem.Models
{
    public class MonthlyPaymentDetailsViewModel
    {
        public string Month { get; set; } = string.Empty;

        public int Year { get; set; }

        public int PaidCount { get; set; }

        public int PendingCount { get; set; }

        public decimal TotalCollected { get; set; }

        public decimal TotalPending { get; set; }

        public decimal CollectionPercent { get; set; }

        public IReadOnlyList<(int Year, string Month, string Label)> AvailablePeriods { get; set; } =
            Array.Empty<(int Year, string Month, string Label)>();

        public IReadOnlyList<MonthlyPaymentRowViewModel> Rows { get; set; } =
            Array.Empty<MonthlyPaymentRowViewModel>();

        public bool CanSendReminders { get; set; }

        public bool CanManagePayments { get; set; }

        public bool UseWhatsAppClickToChat { get; set; }
    }

    public class MonthlyPaymentRowViewModel
    {
        public int? MaintenanceId { get; set; }

        public int ResidentId { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string ResidentName { get; set; } = string.Empty;

        public string MemberLabel { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal Fine { get; set; }

        public decimal TotalDue { get; set; }

        public bool IsPaid { get; set; }

        public string? TransactionId { get; set; }

        public string? PayerName { get; set; }

        public string? PaymentGateway { get; set; }

        public string? ReceiptNumber { get; set; }

        public DateTime? PaidAt { get; set; }

        public string? ResidentPhone { get; set; }

        public string? WhatsAppReminderUrl { get; set; }
    }
}
