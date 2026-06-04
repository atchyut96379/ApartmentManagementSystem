namespace ApartmentManagementSystem.Models
{
    public class LoginCredentialsViewModel
    {
        public int ResidentId { get; set; }

        public string ResidentName { get; set; } = string.Empty;

        public string FlatNumber { get; set; } = string.Empty;

        public string LoginPhone { get; set; } = string.Empty;

        public string? NotificationEmail { get; set; }

        public string TemporaryPassword { get; set; } = string.Empty;

        public bool IsPasswordReset { get; set; }
    }
}
