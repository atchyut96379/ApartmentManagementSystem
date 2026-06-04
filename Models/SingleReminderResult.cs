namespace ApartmentManagementSystem.Models
{
    public class SingleReminderResult
    {
        public bool Sent { get; set; }

        public string? Error { get; set; }

        public string? FlatNumber { get; set; }

        public string? ResidentName { get; set; }
    }
}
