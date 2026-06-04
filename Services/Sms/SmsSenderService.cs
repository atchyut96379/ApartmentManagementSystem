using System.Text;
using System.Text.Json;
using ApartmentManagementSystem.Models;
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

        public async Task<(bool Success, string? Error)> SendAsync(string phone, string body)
        {
            var settings = _settingsStore.GetNotificationSettings();
            if (!settings.IsSmsConfigured)
            {
                return (false, "SMS is not configured. Open Integrations, fill MSG91 or Twilio details, check Enable SMS, and click Save settings.");
            }

            if (settings.SmsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase))
            {
                return await SendMsg91Async(settings, phone, body);
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
                    Encoding.UTF8.GetBytes(
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

        private async Task<(bool Success, string? Error)> SendMsg91Async(
            NotificationSettings settings,
            string phone,
            string body)
        {
            try
            {
                var mobile = NormalizePhoneForMsg91(phone);
                var encodedMessage = Uri.EscapeDataString(body);
                var url =
                    $"https://api.msg91.com/api/v2/sendsms?authkey={Uri.EscapeDataString(settings.Msg91AuthKey)}" +
                    $"&mobiles={mobile}&message={encodedMessage}" +
                    $"&sender={Uri.EscapeDataString(settings.Msg91SenderId)}&route=4&country=91";

                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(url);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, responseText);
                }

                if (responseText.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                    !responseText.Contains("success", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, responseText);
                }

                _logger.LogInformation("MSG91 SMS sent to {Phone}", phone);
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

        private static string NormalizePhoneForMsg91(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return "91" + digits;
            }

            if (digits.StartsWith("91") && digits.Length == 12)
            {
                return digits;
            }

            return digits;
        }
    }
}
