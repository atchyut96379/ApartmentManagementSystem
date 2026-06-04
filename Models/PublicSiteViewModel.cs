namespace ApartmentManagementSystem.Models
{
    public class PublicSiteViewModel
    {
        public string ApartmentName { get; set; } = string.Empty;

        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Address { get; set; }

        public string AppBaseUrl { get; set; } = string.Empty;
    }
}
