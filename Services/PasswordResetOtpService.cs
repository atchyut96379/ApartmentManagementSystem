using ApartmentManagementSystem.Services.Sms;
using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagementSystem.Services
{
    public class PasswordResetOtpService
    {
        private const string CacheKeyPrefix = "pwd-otp:";
        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);

        private readonly IMemoryCache _cache;
        private readonly SmsSenderService _smsSender;
        private readonly IntegrationsSettingsStore _settingsStore;

        public PasswordResetOtpService(
            IMemoryCache cache,
            SmsSenderService smsSender,
            IntegrationsSettingsStore settingsStore)
        {
            _cache = cache;
            _smsSender = smsSender;
            _settingsStore = settingsStore;
        }

        public bool IsSmsOtpAvailable()
        {
            return _settingsStore.GetNotificationSettings().IsSmsConfigured;
        }

        public async Task<(bool Sent, string? Error)> SendOtpAsync(
            string userId,
            string phone,
            string apartmentName)
        {
            if (!IsSmsOtpAvailable())
            {
                return (false, "SMS is not configured. Use mobile + flat verification instead.");
            }

            var code = Random.Shared.Next(100000, 999999).ToString();
            _cache.Set(
                CacheKeyPrefix + userId,
                code,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = OtpLifetime });

            var message =
                $"{apartmentName}: Your password reset code is {code}. Valid for 10 minutes. Do not share.";

            return await _smsSender.SendAsync(phone, message);
        }

        public bool VerifyOtp(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return _cache.TryGetValue(CacheKeyPrefix + userId, out string? stored) &&
                   string.Equals(stored, code.Trim(), StringComparison.Ordinal);
        }

        public void ClearOtp(string userId)
        {
            _cache.Remove(CacheKeyPrefix + userId);
        }
    }
}
