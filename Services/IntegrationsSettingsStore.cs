using System.Text.Json;
using System.Text.Json.Serialization;
using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class IntegrationsSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public IntegrationsSettingsStore(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string LocalFilePath =>
            Path.Combine(_environment.ContentRootPath, "integrations.local.json");

        public bool LocalFileExists => File.Exists(LocalFilePath);

        public NotificationSettings GetNotificationSettings()
        {
            var settings = new NotificationSettings();
            _configuration.GetSection(NotificationSettings.SectionName).Bind(settings);
            ApplyLocalOverlay(settings, LoadLocal()?.Notification);
            NormalizeSmsSettings(settings);
            return settings;
        }

        public PaymentGatewaySettings GetPaymentSettings()
        {
            var settings = new PaymentGatewaySettings();
            _configuration.GetSection(PaymentGatewaySettings.SectionName).Bind(settings);
            var local = LoadLocal()?.Payment;
            if (local != null)
            {
                if (!string.IsNullOrWhiteSpace(local.Provider))
                {
                    settings.Provider = local.Provider;
                }

                if (local.Razorpay != null)
                {
                    OverlayIfSet(local.Razorpay.KeyId, v => settings.Razorpay.KeyId = v);
                    OverlayIfSet(local.Razorpay.KeySecret, v => settings.Razorpay.KeySecret = v);
                    OverlayIfSet(local.Razorpay.WebhookSecret, v => settings.Razorpay.WebhookSecret = v);
                }
            }

            return settings;
        }

        public ApplicationSettings GetApplicationSettings()
        {
            var settings = new ApplicationSettings();
            _configuration.GetSection(ApplicationSettings.SectionName).Bind(settings);
            var local = LoadLocal()?.Application;
            if (local != null && !string.IsNullOrWhiteSpace(local.AppUrl))
            {
                settings.AppUrl = local.AppUrl;
            }

            return settings;
        }

        public IntegrationsConfigureViewModel BuildConfigureViewModel(
            IntegrationStatusViewModel status)
        {
            var notification = GetNotificationSettings();
            var payment = GetPaymentSettings();
            var app = GetApplicationSettings();
            var local = LoadLocal();

            return new IntegrationsConfigureViewModel
            {
                Status = status,
                ConfigFilePath = LocalFilePath,
                ConfigFileExists = LocalFileExists,
                EnableEmail = notification.EnableEmail,
                SmtpHost = notification.SmtpHost,
                SmtpPort = notification.SmtpPort,
                SmtpUseSsl = notification.SmtpUseSsl,
                SmtpUser = notification.SmtpUser,
                FromEmail = notification.FromEmail,
                FromName = notification.FromName,
                EnableSms = notification.EnableSms,
                SmsProvider = notification.SmsProvider,
                TwilioAccountSid = notification.TwilioAccountSid,
                TwilioFromNumber = notification.TwilioFromNumber,
                Msg91AuthKey = MaskIfOnFile(local?.Notification?.Msg91AuthKey),
                Msg91SenderId = notification.Msg91SenderId,
                Msg91FlowId = notification.Msg91FlowId,
                EnableWhatsAppReminders = notification.EnableWhatsAppReminders,
                WhatsAppProvider = notification.WhatsAppProvider,
                Msg91WhatsAppApiEnabled = notification.Msg91WhatsAppApiEnabled,
                Msg91WhatsAppIntegratedNumber = notification.Msg91WhatsAppIntegratedNumber,
                Msg91WhatsAppTemplateName = notification.Msg91WhatsAppTemplateName,
                Msg91WhatsAppTemplateLanguage = notification.Msg91WhatsAppTemplateLanguage,
                Msg91WhatsAppTemplateNamespace = notification.Msg91WhatsAppTemplateNamespace,
                PaymentProvider = payment.Provider,
                RazorpayKeyId = payment.Razorpay.KeyId,
                AppUrl = string.IsNullOrWhiteSpace(app.AppUrl)
                    ? status.AppUrl
                    : app.AppUrl,
                PasswordOnFile = !string.IsNullOrWhiteSpace(local?.Notification?.SmtpPassword),
                RazorpaySecretOnFile = !string.IsNullOrWhiteSpace(local?.Payment?.Razorpay?.KeySecret),
                TwilioTokenOnFile = !string.IsNullOrWhiteSpace(local?.Notification?.TwilioAuthToken),
                Msg91KeyOnFile = !string.IsNullOrWhiteSpace(notification.Msg91AuthKey)
            };
        }

        public async Task SaveAsync(IntegrationsConfigureViewModel model)
        {
            var existing = LoadLocal() ?? new IntegrationsLocalFile();
            existing.Notification ??= new NotificationSettings();
            existing.Payment ??= new PaymentGatewaySettings { Razorpay = new RazorpaySettings() };
            existing.Application ??= new ApplicationSettings();
            existing.Payment.Razorpay ??= new RazorpaySettings();

            existing.Notification.EnableEmail = model.EnableEmail;
            existing.Notification.SmtpHost = model.SmtpHost.Trim();
            existing.Notification.SmtpPort = model.SmtpPort;
            existing.Notification.SmtpUseSsl = model.SmtpUseSsl;
            existing.Notification.SmtpUser = model.SmtpUser?.Trim();
            existing.Notification.FromEmail = model.FromEmail.Trim();
            existing.Notification.FromName = model.FromName.Trim();
            existing.Notification.EnableSms = model.EnableSms;
            existing.Notification.SmsProvider = model.SmsProvider.Trim();
            existing.Notification.TwilioAccountSid = model.TwilioAccountSid?.Trim() ?? string.Empty;
            existing.Notification.TwilioFromNumber = model.TwilioFromNumber?.Trim() ?? string.Empty;
            existing.Notification.Msg91SenderId = model.Msg91SenderId?.Trim() ?? string.Empty;
            existing.Notification.Msg91FlowId = model.Msg91FlowId?.Trim() ?? string.Empty;
            existing.Notification.EnableWhatsAppReminders = model.EnableWhatsAppReminders;
            existing.Notification.WhatsAppProvider = model.WhatsAppProvider.Trim();
            existing.Notification.Msg91WhatsAppApiEnabled = model.Msg91WhatsAppApiEnabled;
            existing.Notification.Msg91WhatsAppIntegratedNumber =
                model.Msg91WhatsAppIntegratedNumber?.Trim() ?? string.Empty;
            existing.Notification.Msg91WhatsAppTemplateName =
                model.Msg91WhatsAppTemplateName?.Trim() ?? string.Empty;
            existing.Notification.Msg91WhatsAppTemplateLanguage =
                string.IsNullOrWhiteSpace(model.Msg91WhatsAppTemplateLanguage)
                    ? "en"
                    : model.Msg91WhatsAppTemplateLanguage.Trim();
            existing.Notification.Msg91WhatsAppTemplateNamespace =
                model.Msg91WhatsAppTemplateNamespace?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
            {
                existing.Notification.SmtpPassword = model.SmtpPassword;
            }

            if (!string.IsNullOrWhiteSpace(model.TwilioAuthToken))
            {
                existing.Notification.TwilioAuthToken = model.TwilioAuthToken;
            }

            if (!string.IsNullOrWhiteSpace(model.Msg91AuthKey) &&
                !model.Msg91AuthKey.StartsWith("••••", StringComparison.Ordinal))
            {
                existing.Notification.Msg91AuthKey = model.Msg91AuthKey.Trim();
            }

            existing.Payment.Provider = model.PaymentProvider.Trim();
            existing.Payment.Razorpay.KeyId = model.RazorpayKeyId?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(model.RazorpayKeySecret))
            {
                existing.Payment.Razorpay.KeySecret = model.RazorpayKeySecret.Trim();
            }

            if (!string.IsNullOrWhiteSpace(model.RazorpayWebhookSecret))
            {
                existing.Payment.Razorpay.WebhookSecret = model.RazorpayWebhookSecret.Trim();
            }

            existing.Application.AppUrl = model.AppUrl.Trim();

            var json = JsonSerializer.Serialize(existing, JsonOptions);
            await File.WriteAllTextAsync(LocalFilePath, json);

            if (_configuration is IConfigurationRoot root)
            {
                root.Reload();
            }
        }

        private IntegrationsLocalFile? LoadLocal()
        {
            if (!LocalFileExists)
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(LocalFilePath);
                return JsonSerializer.Deserialize<IntegrationsLocalFile>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyLocalOverlay(
            NotificationSettings target,
            NotificationSettings? local)
        {
            if (local == null)
            {
                return;
            }

            target.EnableEmail = local.EnableEmail;
            target.EnableSms = local.EnableSms;
            target.EnableWhatsAppReminders = local.EnableWhatsAppReminders;
            target.Msg91WhatsAppApiEnabled = local.Msg91WhatsAppApiEnabled;
            target.SendConfirmationAfterPayment = local.SendConfirmationAfterPayment;
            target.EnableScheduledReminders = local.EnableScheduledReminders;
            target.ReminderHourLocal = local.ReminderHourLocal > 0
                ? local.ReminderHourLocal
                : target.ReminderHourLocal;

            OverlayIfSet(local.SmtpHost, v => target.SmtpHost = v);
            if (local.SmtpPort > 0)
            {
                target.SmtpPort = local.SmtpPort;
            }

            target.SmtpUseSsl = local.SmtpUseSsl;
            OverlayIfSet(local.SmtpUser, v => target.SmtpUser = v);
            OverlayIfSet(local.SmtpPassword, v => target.SmtpPassword = v);
            OverlayIfSet(local.FromEmail, v => target.FromEmail = v);
            OverlayIfSet(local.FromName, v => target.FromName = v);
            OverlayIfSet(local.SmsProvider, v => target.SmsProvider = v);
            OverlayIfSet(local.TwilioAccountSid, v => target.TwilioAccountSid = v);
            OverlayIfSet(local.TwilioAuthToken, v => target.TwilioAuthToken = v);
            OverlayIfSet(local.TwilioFromNumber, v => target.TwilioFromNumber = v);
            OverlayIfSet(local.Msg91AuthKey, v => target.Msg91AuthKey = v);
            OverlayIfSet(local.Msg91SenderId, v => target.Msg91SenderId = v);
            OverlayIfSet(local.Msg91FlowId, v => target.Msg91FlowId = v);
            OverlayIfSet(local.WhatsAppProvider, v => target.WhatsAppProvider = v);
            OverlayIfSet(local.Msg91WhatsAppIntegratedNumber, v => target.Msg91WhatsAppIntegratedNumber = v);
            OverlayIfSet(local.Msg91WhatsAppTemplateName, v => target.Msg91WhatsAppTemplateName = v);
            OverlayIfSet(local.Msg91WhatsAppTemplateLanguage, v => target.Msg91WhatsAppTemplateLanguage = v);
            OverlayIfSet(local.Msg91WhatsAppTemplateNamespace, v => target.Msg91WhatsAppTemplateNamespace = v);
        }

        private static void OverlayIfSet(string? value, Action<string> apply)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                apply(value);
            }
        }

        private static string? MaskIfOnFile(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : "••••••••";
        }

        /// <summary>
        /// Turns on MSG91 when keys exist (Azure app settings or integrations.local.json).
        /// </summary>
        private static void NormalizeSmsSettings(NotificationSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SmsProvider))
            {
                settings.SmsProvider = "Msg91";
            }

            var hasMsg91 = !string.IsNullOrWhiteSpace(settings.Msg91AuthKey) &&
                           !string.IsNullOrWhiteSpace(settings.Msg91SenderId);

            if (hasMsg91)
            {
                settings.EnableSms = true;
                settings.SmsProvider = "Msg91";
            }
        }
    }
}
