using System.Text;
using System.Text.Json;
using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Logging;

namespace ApartmentManagementSystem.Services.WhatsApp
{
    internal static class Msg91WhatsAppClient
    {
        public static async Task<(bool Success, string? Error)> SendTemplateReminderAsync(
            HttpClient client,
            NotificationSettings settings,
            string phone,
            IReadOnlyDictionary<string, string> bodyVariables,
            ILogger logger)
        {
            var integrated = WhatsAppLinkBuilder.NormalizeIndianMobile(settings.Msg91WhatsAppIntegratedNumber);
            var recipient = WhatsAppLinkBuilder.NormalizeIndianMobile(phone);
            if (integrated == null || recipient == null)
            {
                return (false, "Invalid WhatsApp integrated number or resident mobile.");
            }

            var components = BuildBodyComponents(bodyVariables);
            var template = new Dictionary<string, object>
            {
                ["name"] = settings.Msg91WhatsAppTemplateName.Trim(),
                ["language"] = new Dictionary<string, object>
                {
                    ["code"] = string.IsNullOrWhiteSpace(settings.Msg91WhatsAppTemplateLanguage)
                        ? "en"
                        : settings.Msg91WhatsAppTemplateLanguage.Trim(),
                    ["policy"] = "deterministic"
                },
                ["to_and_components"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["to"] = new[] { recipient },
                        ["components"] = components
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(settings.Msg91WhatsAppTemplateNamespace))
            {
                template["namespace"] = settings.Msg91WhatsAppTemplateNamespace.Trim();
            }

            var payload = new Dictionary<string, object>
            {
                ["integrated_number"] = integrated,
                ["content_type"] = "template",
                ["payload"] = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["type"] = "template",
                    ["template"] = template
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://control.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.TryAddWithoutValidation("authkey", settings.Msg91AuthKey.Trim());
            request.Headers.TryAddWithoutValidation("accept", "application/json");

            var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            logger.LogInformation(
                "MSG91 WhatsApp API response for {Phone}: {Response}",
                phone,
                responseText);

            return InterpretResponse(response.IsSuccessStatusCode, responseText);
        }

        private static Dictionary<string, object> BuildBodyComponents(
            IReadOnlyDictionary<string, string> variables)
        {
            var ordered = new[]
            {
                GetVariable(variables, "name"),
                GetVariable(variables, "month"),
                GetVariable(variables, "link")
            };

            var components = new Dictionary<string, object>();
            for (var i = 0; i < ordered.Length; i++)
            {
                components[$"body_{i + 1}"] = new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["value"] = ordered[i]
                };
            }

            return components;
        }

        private static string GetVariable(IReadOnlyDictionary<string, string> variables, string key) =>
            variables.TryGetValue(key, out var value) ? value : string.Empty;

        private static (bool Success, string? Error) InterpretResponse(
            bool httpOk,
            string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return httpOk
                    ? (true, null)
                    : (false, "MSG91 WhatsApp API returned an empty response.");
            }

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString()?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return (false, ExtractMessage(root) ?? responseText);
                }

                if (root.TryGetProperty("hasError", out var hasError) &&
                    (hasError.ValueKind == JsonValueKind.True ||
                     (hasError.ValueKind == JsonValueKind.String &&
                      hasError.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)))
                {
                    return (false, ExtractMessage(root) ?? responseText);
                }

                if (root.TryGetProperty("status", out var statusProp))
                {
                    var status = statusProp.GetString();
                    if (status != null &&
                        (status.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
                         status.Contains("error", StringComparison.OrdinalIgnoreCase)))
                    {
                        return (false, ExtractMessage(root) ?? responseText);
                    }
                }
            }
            catch (JsonException)
            {
                var lower = responseText.ToLowerInvariant();
                if (lower.Contains("\"type\":\"error\"") || lower.Contains("invalid template"))
                {
                    return (false, Truncate(responseText, 500));
                }
            }

            return httpOk ? (true, null) : (false, Truncate(responseText, 500));
        }

        private static string? ExtractMessage(JsonElement root)
        {
            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                return errors[0].ToString();
            }

            return null;
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
