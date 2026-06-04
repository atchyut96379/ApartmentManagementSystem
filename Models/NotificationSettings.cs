namespace ApartmentManagementSystem.Models
{
    public class NotificationSettings
    {
        public const string SectionName = "Notification";

        public bool EnableScheduledReminders { get; set; } = true;

        public int ReminderHourLocal { get; set; } = 9;

        public bool SendConfirmationAfterPayment { get; set; } = true;

        public bool EnableEmail { get; set; }

        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        public bool SmtpUseSsl { get; set; } = true;

        public string? SmtpUser { get; set; }

        public string? SmtpPassword { get; set; }

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "Apartment Management";

        /// <summary>Msg91, Twilio, or Simulation (log only — testing)</summary>
        public string SmsProvider { get; set; } = "Msg91";

        public bool EnableSms { get; set; }

        public string TwilioAccountSid { get; set; } = string.Empty;

        public string TwilioAuthToken { get; set; } = string.Empty;

        public string TwilioFromNumber { get; set; } = string.Empty;

        public string Msg91AuthKey { get; set; } = string.Empty;

        public string Msg91SenderId { get; set; } = string.Empty;

        /// <summary>MSG91 Flow / Template ID (India DLT). Required for delivery on most Indian accounts.</summary>
        public string Msg91FlowId { get; set; } = string.Empty;

        /// <summary>Payment reminders via WhatsApp (recommended while SMS DLT is pending).</summary>
        public bool EnableWhatsAppReminders { get; set; } = true;

        /// <summary>ClickToChat (open wa.me — uses association WhatsApp), Msg91 (API), or Simulation.</summary>
        public string WhatsAppProvider { get; set; } = "ClickToChat";

        /// <summary>MSG91 WhatsApp Business integrated number with country code, e.g. 9198xxxxxxxx.</summary>
        public string Msg91WhatsAppIntegratedNumber { get; set; } = string.Empty;

        public string Msg91WhatsAppTemplateName { get; set; } = string.Empty;

        public string Msg91WhatsAppTemplateLanguage { get; set; } = "en";

        /// <summary>Optional Meta template namespace from MSG91 template details.</summary>
        public string Msg91WhatsAppTemplateNamespace { get; set; } = string.Empty;

        /// <summary>
        /// When false (default), reminders use Click-to-Chat links only — no MSG91 WhatsApp API.
        /// Turn on after templates appear in MSG91 Send WhatsApp dropdown.
        /// </summary>
        public bool Msg91WhatsAppApiEnabled { get; set; }

        public bool IsEmailConfigured =>
            EnableEmail &&
            !string.IsNullOrWhiteSpace(SmtpHost) &&
            !string.IsNullOrWhiteSpace(FromEmail) &&
            !string.IsNullOrWhiteSpace(SmtpUser) &&
            !string.IsNullOrWhiteSpace(SmtpPassword);

        public bool IsTwilioConfigured =>
            EnableSms &&
            SmsProvider.Equals("Twilio", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(TwilioAccountSid) &&
            !string.IsNullOrWhiteSpace(TwilioAuthToken) &&
            !string.IsNullOrWhiteSpace(TwilioFromNumber);

        public bool IsMsg91FlowConfigured =>
            !string.IsNullOrWhiteSpace(Msg91FlowId);

        public bool IsMsg91Configured =>
            EnableSms &&
            SmsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Msg91AuthKey) &&
            !string.IsNullOrWhiteSpace(Msg91SenderId);

        public bool IsSimulationSms =>
            EnableSms &&
            SmsProvider.Equals("Simulation", StringComparison.OrdinalIgnoreCase);

        public bool IsSmsConfigured =>
            IsTwilioConfigured || IsMsg91Configured || IsSimulationSms;

        public bool IsClickToChatWhatsApp =>
            EnableWhatsAppReminders &&
            WhatsAppProvider.Equals("ClickToChat", StringComparison.OrdinalIgnoreCase);

        /// <summary>Manual WhatsApp from association phone (wa.me) — used while MSG91 templates are unavailable.</summary>
        public bool UseWhatsAppClickToChatForReminders =>
            EnableWhatsAppReminders &&
            (IsClickToChatWhatsApp || !Msg91WhatsAppApiEnabled);

        public bool IsSimulationWhatsApp =>
            EnableWhatsAppReminders &&
            WhatsAppProvider.Equals("Simulation", StringComparison.OrdinalIgnoreCase);

        public bool IsMsg91WhatsAppConfigured =>
            EnableWhatsAppReminders &&
            Msg91WhatsAppApiEnabled &&
            WhatsAppProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Msg91AuthKey) &&
            !string.IsNullOrWhiteSpace(Msg91WhatsAppIntegratedNumber) &&
            !string.IsNullOrWhiteSpace(Msg91WhatsAppTemplateName);

        public bool CanAutomatePaymentReminders =>
            IsMsg91WhatsAppConfigured ||
            (IsSmsConfigured && !IsSimulationSms && IsMsg91FlowConfigured);
    }
}
