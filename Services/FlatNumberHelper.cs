namespace ApartmentManagementSystem.Services
{
    public static class FlatNumberHelper
    {
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
    }
}
