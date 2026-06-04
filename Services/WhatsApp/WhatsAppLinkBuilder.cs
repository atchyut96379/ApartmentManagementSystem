using System.Text;

namespace ApartmentManagementSystem.Services.WhatsApp
{
    public static class WhatsAppLinkBuilder
    {
        public static string? BuildClickToChatUrl(string? phone, string message)
        {
            var digits = NormalizeIndianMobile(phone);
            if (digits == null || string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var encoded = Uri.EscapeDataString(message.Trim());
            return $"https://wa.me/{digits}?text={encoded}";
        }

        public static string? NormalizeIndianMobile(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return "91" + digits;
            }

            if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            {
                return digits;
            }

            if (digits.Length > 10 && digits.StartsWith("91", StringComparison.Ordinal))
            {
                return digits;
            }

            return digits.Length >= 10 ? digits : null;
        }
    }
}
