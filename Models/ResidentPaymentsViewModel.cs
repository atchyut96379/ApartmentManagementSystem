namespace ApartmentManagementSystem.Models
{
    public class ResidentPaymentsViewModel
    {
        public bool HasResidentProfile { get; set; }

        public string? FlatNumber { get; set; }

        public int TotalAssignedCount { get; set; }

        public int PendingCount { get; set; }

        public IReadOnlyList<Maintenance> Payments { get; set; } =
            Array.Empty<Maintenance>();

        public bool PendingOnly { get; set; }

        public bool ShowReceiptColumns { get; set; }

        public string ReturnUrl { get; set; } = "/";
    }
}
