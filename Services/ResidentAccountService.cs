using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Options;

namespace ApartmentManagementSystem.Services
{
    public class ResidentAccountService
    {
        private readonly SocietySettings _societySettings;

        public ResidentAccountService(IOptions<SocietySettings> societySettings)
        {
            _societySettings = societySettings.Value;
        }

        /// <summary>
        /// Default resident password: first 4 letters of apartment name + up to 3 flat digits
        /// (e.g. Marv507, Marv109 for flat 109-110).
        /// </summary>
        public string GenerateDefaultPassword(string flatNumber)
        {
            var prefix = GetApartmentNamePrefix();
            var flatPart = NormalizeFlatForPassword(flatNumber);
            return prefix + flatPart;
        }

        public string DescribeDefaultPasswordFormat()
        {
            var prefix = GetApartmentNamePrefix();
            return $"{prefix}[flat — max 3 digits, first part if 109-110] (e.g. {prefix}507, {prefix}109)";
        }

        private string GetApartmentNamePrefix()
        {
            var lettersOnly = new string(
                (_societySettings.ApartmentName ?? string.Empty)
                    .Where(char.IsLetter)
                    .ToArray());

            if (lettersOnly.Length == 0)
            {
                lettersOnly = "Apt";
            }

            var four = lettersOnly.Length >= 4
                ? lettersOnly[..4]
                : lettersOnly.PadRight(4, 'x');

            return char.ToUpperInvariant(four[0]) +
                   four[1..].ToLowerInvariant();
        }

        private const int MaxFlatDigitsInPassword = 3;

        private static string NormalizeFlatForPassword(string? flatNumber)
        {
            if (string.IsNullOrWhiteSpace(flatNumber))
            {
                return string.Empty;
            }

            var trimmed = flatNumber
                .Trim()
                .Replace("flat", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var primary = trimmed.Contains('-')
                ? trimmed.Split('-', 2)[0].Trim()
                : trimmed;

            var digits = new string(primary.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                digits = new string(primary.Where(char.IsLetterOrDigit).ToArray());
            }

            if (digits.Length == 0)
            {
                return string.Empty;
            }

            return digits.Length <= MaxFlatDigitsInPassword
                ? digits
                : digits[..MaxFlatDigitsInPassword];
        }
    }
}
