using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string? ActorUserId { get; set; }

        [MaxLength(200)]
        public string ActorDisplayName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EntityType { get; set; }

        public int? EntityId { get; set; }

        [MaxLength(50)]
        public string? FlatNumber { get; set; }

        [MaxLength(2000)]
        public string Details { get; set; } = string.Empty;
    }

    public static class AuditActions
    {
        public const string LoginCreated = "LoginCreated";
        public const string PasswordReset = "PasswordReset";
        public const string DesignationChanged = "DesignationChanged";
        public const string ResidentsImported = "ResidentsImported";
        public const string PaymentRecorded = "PaymentRecorded";
        public const string PaymentReminderSent = "PaymentReminderSent";
        public const string OfflinePaymentMarked = "OfflinePaymentMarked";
    }
}
