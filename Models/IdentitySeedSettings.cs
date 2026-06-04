namespace ApartmentManagementSystem.Models
{
    public class IdentitySeedSettings
    {
        public const string SectionName = "Identity";

        /// <summary>When true, deletes all users and creates only the system Admin account on startup.</summary>
        public bool ResetAndSeedAdminOnStartup { get; set; }
    }
}
