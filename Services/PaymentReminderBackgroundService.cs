using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class PaymentReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentReminderBackgroundService> _logger;
        private DateTime? _lastRunDate;

        public PaymentReminderBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PaymentReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var settingsStore = scope.ServiceProvider
                        .GetRequiredService<IntegrationsSettingsStore>();
                    var notificationSettings = settingsStore.GetNotificationSettings();

                    if (notificationSettings.EnableScheduledReminders &&
                        DateTime.Now.Hour == notificationSettings.ReminderHourLocal &&
                        _lastRunDate != DateTime.Today)
                    {
                        var notifications = scope.ServiceProvider
                            .GetRequiredService<PaymentNotificationService>();
                        var result = await notifications.SendPendingPaymentRemindersAsync();
                        _lastRunDate = DateTime.Today;
                        _logger.LogInformation(
                            "Payment reminders sent. Email={Email}, SMS={Sms}, Skipped={Skipped}",
                            result.EmailSent,
                            result.SmsSent,
                            result.Skipped);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Payment reminder job failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
