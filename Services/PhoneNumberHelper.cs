namespace ApartmentManagementSystem.Services
{
    public static class PhoneNumberHelper
    {
        public static string NormalizeDigits(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return string.Empty;
            }

            return new string(phone.Where(char.IsDigit).ToArray());
        }

        public static bool Match(string? stored, string? provided)
        {
            var a = NormalizeDigits(stored);
            var b = NormalizeDigits(provided);

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            if (a.Equals(b, StringComparison.Ordinal))
            {
                return true;
            }

            if (a.Length >= 10 && b.Length >= 10)
            {
                return a.EndsWith(b, StringComparison.Ordinal) ||
                       b.EndsWith(a, StringComparison.Ordinal);
            }

            return false;
        }
    }
}
