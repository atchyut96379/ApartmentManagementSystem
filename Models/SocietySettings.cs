namespace ApartmentManagementSystem.Models
{
    public class SocietySettings
    {
        public const string SectionName = "Society";

        /// <summary>Society / apartment display name (e.g. Marvel Rocks).</summary>
        public string ApartmentName { get; set; } = "Marvel Rocks";

        /// <summary>Domain for auto-generated resident login emails (e.g. marvelrocks.local).</summary>
        public string LoginEmailDomain { get; set; } = "marvelrocks.local";

        /// <summary>Shown on public legal pages (Razorpay / resident contact).</summary>
        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Address { get; set; }
    }
}
