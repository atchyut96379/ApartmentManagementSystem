using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Logging;

namespace ApartmentManagementSystem.Services.WhatsApp
{
    public class WhatsAppSenderService
    {
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WhatsAppSenderService> _logger;

        public WhatsAppSenderService(
            IntegrationsSettingsStore settingsStore,
            IHttpClientFactory httpClientFactory,
            ILogger<WhatsAppSenderService> logger)
        {
            _settingsStore = settingsStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public bool IsClickToChatMode()
        {
            var settings = _settingsStore.GetNotificationSettings();
            return settings.EnableWhatsAppReminders &&
                   settings.IsClickToChatWhatsApp;
        }

        public async Task<(bool Success, string? Error)> SendReminderAsync(
            string phone,
            IReadOnlyDictionary<string, string> bodyVariables,
            string plainTextFallback)
        {
            var settings = _settingsStore.GetNotificationSettings();
            if (!settings.EnableWhatsAppReminders)
            {
                return (false, "WhatsApp reminders are disabled in Integrations.");
            }

            if (settings.UseWhatsAppClickToChatForReminders)
            {
                return (false,
                    "MSG91 WhatsApp API is off. Use WhatsApp links on the collection dashboard.");
            }

            if (settings.IsSimulationWhatsApp)
            {
                _logger.LogWarning(
                    "WhatsApp simulation (no real message) to {Phone}: {Body}",
                    phone,
                    plainTextFallback);
                return (true, null);
            }

            if (!settings.IsMsg91WhatsAppConfigured)
            {
                return (false,
                    "Configure MSG91 WhatsApp: integrated number + approved template name under Integrations.");
            }

            var client = _httpClientFactory.CreateClient();
            return await Msg91WhatsAppClient.SendTemplateReminderAsync(
                client,
                settings,
                phone,
                bodyVariables,
                _logger);
        }
    }
}
