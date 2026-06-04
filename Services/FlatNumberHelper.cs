using ApartmentManagementSystem.Models;

namespace ApartmentManagementSystem.Services
{
    public static class FlatNumberHelper
    {
        private static readonly Dictionary<string, string> SocietyCombinedFlats =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["109"] = "109-110",
                ["110"] = "109-110",
                ["209"] = "209-210",
                ["210"] = "209-210",
                ["309"] = "309-310",
                ["310"] = "309-310",
                ["409"] = "409-410",
                ["410"] = "409-410",
                ["509"] = "509-510",
                ["510"] = "509-510"
            };

        public static Resident? FindResidentForFlat(
            IEnumerable<Resident> residents,
            string? flatNumber)
        {
            return residents.FirstOrDefault(r => Match(r.FlatNumber, flatNumber));
        }

        public static bool Match(string? maintenanceFlat, string? residentFlat)
        {
            if (string.IsNullOrWhiteSpace(maintenanceFlat) ||
                string.IsNullOrWhiteSpace(residentFlat))
            {
                return false;
            }

            if (maintenanceFlat.Trim()
                .Equals(residentFlat.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Normalize(maintenanceFlat) == Normalize(residentFlat);
        }

        public static string Normalize(string? flat)
        {
            if (string.IsNullOrWhiteSpace(flat))
            {
                return string.Empty;
            }

            return flat.Trim()
                .Replace("flat", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-", "")
                .Replace(" ", "")
                .ToUpperInvariant();
        }

        public static string NormalizeSocietyFlatNumber(string? flat)
        {
            if (string.IsNullOrWhiteSpace(flat))
            {
                return string.Empty;
            }

            var trimmed = flat.Trim();
            if (SocietyCombinedFlats.TryGetValue(trimmed, out var combined))
            {
                return combined;
            }

            return trimmed;
        }
    }
}
