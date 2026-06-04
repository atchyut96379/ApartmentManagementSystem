using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services.WhatsApp;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class PaymentReminderMessageBuilder
    {
        private readonly SocietySettings _society;

        public PaymentReminderMessageBuilder(IOptions<SocietySettings> society)
        {
            _society = society.Value;
        }

        public string BuildReminderText(
            string residentName,
            string month,
            string payUrl)
        {
            var firstName = GetFirstName(residentName);
            var associationName = GetAssociationName();

            return
                $"Hi {firstName}, Still {month} maintenance is pending. Pay: {payUrl} From {associationName}";
        }

        public string BuildReminderSmsBody(
            string residentName,
            string month,
            string payUrl)
        {
            var firstName = GetFirstName(residentName);
            var associationName = GetAssociationName();

            return
                $"Hi {firstName},\n" +
                $"Still {month} maintenance is pending.\n" +
                $"Pay: {payUrl}\n" +
                $"From {associationName}";
        }

        public string BuildPayUrl(
            IntegrationsSettingsStore settingsStore,
            Microsoft.AspNetCore.Http.HttpRequest? request)
        {
            var appUrl = settingsStore.GetApplicationSettings().GetAppUrl(request);
            return $"{appUrl.TrimEnd('/')}/ResidentPayments/MyPayments";
        }

        public string? BuildClickToChatUrl(string? phone, string reminderText) =>
            WhatsAppLinkBuilder.BuildClickToChatUrl(phone, reminderText);

        public Dictionary<string, string> BuildTemplateVariables(
            string residentName,
            string month,
            string payUrl) =>
            new()
            {
                ["name"] = GetFirstName(residentName),
                ["month"] = month,
                ["link"] = payUrl
            };

        private string GetAssociationName()
        {
            var name = (_society.ApartmentName ?? "Marvel").Trim();
            if (name.EndsWith("association", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            var shortName = name.Replace(" Rocks", "", StringComparison.OrdinalIgnoreCase).Trim();
            return $"{shortName} association";
        }

        private static string GetFirstName(string residentName)
        {
            var firstName = residentName.Trim();
            var space = firstName.IndexOf(' ');
            if (space > 0)
            {
                firstName = firstName[..space];
            }

            return firstName;
        }
    }
}
