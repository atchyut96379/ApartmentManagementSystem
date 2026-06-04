using ApartmentManagementSystem.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ApartmentManagementSystem.Services.Email
{
    public class SmtpEmailSender
    {
        private readonly IntegrationsSettingsStore _settingsStore;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(
            IntegrationsSettingsStore settingsStore,
            ILogger<SmtpEmailSender> logger)
        {
            _settingsStore = settingsStore;
            _logger = logger;
        }

        public async Task<(bool Success, string? Error)> SendAsync(
            string to,
            string subject,
            string plainText,
            string? htmlBody = null)
        {
            var settings = _settingsStore.GetNotificationSettings();
            if (!settings.IsEmailConfigured)
            {
                return (false, "Email is not configured. Open Integrations, fill SMTP details, check Enable email, and click Save settings.");
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder
                {
                    TextBody = plainText,
                    HtmlBody = htmlBody ?? plainText.Replace("\n", "<br/>")
                };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                var secureSocket = settings.SmtpPort == 465
                    ? SecureSocketOptions.SslOnConnect
                    : settings.SmtpUseSsl
                        ? SecureSocketOptions.StartTls
                        : SecureSocketOptions.None;

                await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureSocket);
                await client.AuthenticateAsync(settings.SmtpUser!, settings.SmtpPassword!);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                return (false, ex.Message);
            }
        }
    }
}
