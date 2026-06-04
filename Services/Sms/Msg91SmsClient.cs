using System.Text;
using System.Text.Json;
using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Logging;

namespace ApartmentManagementSystem.Services.Sms
{
    internal static class Msg91SmsClient
    {
        public static async Task<(bool Success, string? Error)> SendAsync(
            HttpClient client,
            NotificationSettings settings,
            string phone,
            string body,
            IReadOnlyDictionary<string, string>? flowVariables,
            ILogger logger)
        {
            if (!string.IsNullOrWhiteSpace(settings.Msg91FlowId) && flowVariables != null)
            {
                return await SendFlowAsync(client, settings, phone, flowVariables, logger);
            }

            return await SendLegacyAsync(client, settings, phone, body, logger);
        }

        private static async Task<(bool Success, string? Error)> SendFlowAsync(
            HttpClient client,
            NotificationSettings settings,
            string phone,
            IReadOnlyDictionary<string, string> variables,
            ILogger logger)
        {
            var mobile = NormalizePhoneForMsg91(phone);
            var recipient = new Dictionary<string, object>
            {
                ["mobiles"] = mobile
            };

            foreach (var pair in variables)
            {
                recipient[pair.Key] = pair.Value;
            }

            var payload = new Dictionary<string, object>
            {
                ["flow_id"] = settings.Msg91FlowId.Trim(),
                ["sender"] = settings.Msg91SenderId.Trim(),
                ["recipients"] = new[] { recipient }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.msg91.com/api/v5/flow/")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.TryAddWithoutValidation("authkey", settings.Msg91AuthKey.Trim());

            var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            logger.LogInformation(
                "MSG91 Flow API response for {Phone}: {Response}",
                phone,
                responseText);

            return InterpretResponse(response.IsSuccessStatusCode, responseText);
        }

        private static async Task<(bool Success, string? Error)> SendLegacyAsync(
            HttpClient client,
            NotificationSettings settings,
            string phone,
            string body,
            ILogger logger)
        {
            var mobile = NormalizePhoneForMsg91(phone);
            var encodedMessage = Uri.EscapeDataString(body);
            var url =
                $"https://api.msg91.com/api/v2/sendsms?authkey={Uri.EscapeDataString(settings.Msg91AuthKey)}" +
                $"&mobiles={mobile}&message={encodedMessage}" +
                $"&sender={Uri.EscapeDataString(settings.Msg91SenderId)}&route=4&country=91";

            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();
            logger.LogInformation(
                "MSG91 v2 API response for {Phone}: {Response}",
                phone,
                responseText);

            var result = InterpretResponse(response.IsSuccessStatusCode, responseText);
            if (result.Success)
            {
                logger.LogWarning(
                    "MSG91 v2 accepted the request. For India DLT accounts, add Notification__Msg91FlowId " +
                    "with an approved MSG91 template or SMS may not be delivered.");
            }

            return result;
        }

        private static (bool Success, string? Error) InterpretResponse(
            bool httpOk,
            string responseText)
        {
            if (!httpOk)
            {
                return (false, Truncate(responseText, 500));
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return (true, null);
            }

            var lower = responseText.ToLowerInvariant();
            if (lower.Contains("\"type\":\"error\"") ||
                lower.Contains("\"type\": \"error\"") ||
                lower.Contains("\"message\":\"error\"") ||
                lower.Contains("invalid flow") ||
                lower.Contains("dlt"))
            {
                return (false, Truncate(responseText, 500));
            }

            return (true, null);
        }

        private static string Truncate(string text, int max)
        {
            if (text.Length <= max)
            {
                return text;
            }

            return text[..max] + "...";
        }

        public static string NormalizePhoneForMsg91(string phone)
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
