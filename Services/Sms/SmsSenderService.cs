using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Logging;

namespace ApartmentManagementSystem.Services.Sms
{
    public class SmsSenderService
    {
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SmsSenderService> _logger;

        public SmsSenderService(
            IntegrationsSettingsStore settingsStore,
            IHttpClientFactory httpClientFactory,
            ILogger<SmsSenderService> logger)
        {
            _settingsStore = settingsStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<(bool Success, string? Error)> SendAsync(string phone, string body) =>
            SendAsync(phone, body, flowVariables: null);

        public async Task<(bool Success, string? Error)> SendAsync(
            string phone,
            string body,
            IReadOnlyDictionary<string, string>? flowVariables)
        {
            var settings = _settingsStore.GetNotificationSettings();
            if (!settings.IsSmsConfigured)
            {
                return (false,
                    "SMS is not configured. Integrations → enable SMS → MSG91 Auth Key + Sender ID → Save. " +
                    "On Azure, also add Notification__Msg91AuthKey and Notification__Msg91SenderId in App Service settings.");
            }

            if (settings.IsSimulationSms)
            {
                _logger.LogWarning(
                    "SMS simulation (no real message sent) to {Phone}: {Body}",
                    phone,
                    body);
                return (true, null);
            }

            if (settings.SmsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient();
                return await Msg91SmsClient.SendAsync(
                    client,
                    settings,
                    phone,
                    body,
                    flowVariables,
                    _logger);
            }

            return await SendTwilioAsync(settings, phone, body);
        }

        private async Task<(bool Success, string? Error)> SendTwilioAsync(
            NotificationSettings settings,
            string phone,
            string body)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{settings.TwilioAccountSid}:{settings.TwilioAuthToken}"));

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var normalizedPhone = NormalizePhone(phone);
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = normalizedPhone,
                    ["From"] = settings.TwilioFromNumber,
                    ["Body"] = body
                });

                var response = await client.PostAsync(
                    $"https://api.twilio.com/2010-04-01/Accounts/{settings.TwilioAccountSid}/Messages.json",
                    form);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, error);
                }

                _logger.LogInformation("Twilio SMS sent to {Phone}", phone);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return "+91" + digits;
            }

            return phone.StartsWith('+') ? phone : "+" + digits;
        }
    }
}
