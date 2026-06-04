using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class IntegrationsConfigureViewModel
    {
        public IntegrationStatusViewModel Status { get; set; } = new();

        public string ConfigFilePath { get; set; } = string.Empty;

        public bool ConfigFileExists { get; set; }

        [Display(Name = "Enable email")]
        public bool EnableEmail { get; set; }

        public string SmtpHost { get; set; } = "smtp.gmail.com";

        public int SmtpPort { get; set; } = 587;

        public bool SmtpUseSsl { get; set; } = true;

        public string? SmtpUser { get; set; }

        [Display(Name = "SMTP password (app password)")]
        public string? SmtpPassword { get; set; }

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "Apartment Management";

        [Display(Name = "Enable SMS")]
        public bool EnableSms { get; set; }

        public string SmsProvider { get; set; } = "Msg91";

        public string? TwilioAccountSid { get; set; }

        public string? TwilioAuthToken { get; set; }

        public string? TwilioFromNumber { get; set; }

        public string? Msg91AuthKey { get; set; }

        public string? Msg91SenderId { get; set; }

        [Display(Name = "MSG91 Flow / Template ID (India DLT)")]
        public string? Msg91FlowId { get; set; }

        [Display(Name = "Enable WhatsApp reminders")]
        public bool EnableWhatsAppReminders { get; set; } = true;

        public string WhatsAppProvider { get; set; } = "ClickToChat";

        [Display(Name = "Enable MSG91 WhatsApp API (automatic send)")]
        public bool Msg91WhatsAppApiEnabled { get; set; }

        [Display(Name = "MSG91 WhatsApp number (91xxxxxxxxxx)")]
        public string? Msg91WhatsAppIntegratedNumber { get; set; }

        [Display(Name = "MSG91 WhatsApp template name")]
        public string? Msg91WhatsAppTemplateName { get; set; }

        public string? Msg91WhatsAppTemplateLanguage { get; set; }

        [Display(Name = "MSG91 WhatsApp template namespace (optional)")]
        public string? Msg91WhatsAppTemplateNamespace { get; set; }

        public string PaymentProvider { get; set; } = "Simulation";

        public string? RazorpayKeyId { get; set; }

        public string? RazorpayKeySecret { get; set; }

        public string? RazorpayWebhookSecret { get; set; }

        public string AppUrl { get; set; } = string.Empty;

        public bool PasswordOnFile { get; set; }

        public bool RazorpaySecretOnFile { get; set; }

        public bool TwilioTokenOnFile { get; set; }

        public bool Msg91KeyOnFile { get; set; }
    }
}
