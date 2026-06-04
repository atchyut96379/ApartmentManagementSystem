namespace ApartmentManagementSystem.Models
{
    public class IdentitySeedSettings
    {
        public const string SectionName = "Identity";

        /// <summary>When true, deletes all users and creates only the system Admin account on startup.</summary>
        public bool ResetAndSeedAdminOnStartup { get; set; }

        public int PasswordRequiredLength { get; set; } = 8;

        public bool PasswordRequireDigit { get; set; } = true;

        public int LockoutMaxFailedAttempts { get; set; } = 5;

        public int LockoutMinutes { get; set; } = 15;

        public int SessionIdleMinutes { get; set; } = 60;

        /// <summary>New system Admin account must change password on first login.</summary>
        public bool RequireAdminPasswordChangeOnFirstLogin { get; set; } = true;
    }
}
