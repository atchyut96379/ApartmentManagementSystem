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

        /// <summary>Twilio or Msg91 (India)</summary>
        public string SmsProvider { get; set; } = "Twilio";

        public bool EnableSms { get; set; }

        public string TwilioAccountSid { get; set; } = string.Empty;

        public string TwilioAuthToken { get; set; } = string.Empty;

        public string TwilioFromNumber { get; set; } = string.Empty;

        public string Msg91AuthKey { get; set; } = string.Empty;

        public string Msg91SenderId { get; set; } = string.Empty;

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

        public bool IsMsg91Configured =>
            EnableSms &&
            SmsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Msg91AuthKey) &&
            !string.IsNullOrWhiteSpace(Msg91SenderId);

        public bool IsSmsConfigured => IsTwilioConfigured || IsMsg91Configured;
    }
}
