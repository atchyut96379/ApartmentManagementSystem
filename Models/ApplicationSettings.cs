using Microsoft.AspNetCore.Http;

namespace ApartmentManagementSystem.Models
{
    public class ApplicationSettings
    {
        public const string SectionName = "Application";

        /// <summary>Public URL of the site (e.g. https://yourdomain.com) — used in emails/SMS links.</summary>
        public string AppUrl { get; set; } = string.Empty;

        public string GetAppUrl(HttpRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(AppUrl))
            {
                return AppUrl.TrimEnd('/');
            }

            if (request != null)
            {
                return $"{request.Scheme}://{request.Host}";
            }

            return "https://localhost:7119";
        }
    }
}
