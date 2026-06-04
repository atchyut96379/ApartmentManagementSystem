namespace ApartmentManagementSystem.Models
{
    public class CommitteeMemberRowViewModel
    {
        public string Designation { get; set; } = string.Empty;

        public bool IsFilled { get; set; }

        public int? ResidentId { get; set; }

        public string? Name { get; set; }

        public string? FlatNumber { get; set; }

        public string? LoginPhone { get; set; }

        public string? NotificationEmail { get; set; }

        public bool HasLogin { get; set; }
    }
}
