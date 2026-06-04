using System.Globalization;

namespace ApartmentManagementSystem.Services
{
    public static class BillingMonthHelper
    {
        public static string GetMonthName(int month) =>
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

        public static IReadOnlyList<(int Year, string Month, string Label)> GetRecentBillingPeriods(int count = 12)
        {
            var list = new List<(int Year, string Month, string Label)>();
            var cursor = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            for (var i = 0; i < count; i++)
            {
                var monthName = GetMonthName(cursor.Month);
                list.Add((cursor.Year, monthName, $"{monthName} {cursor.Year}"));
                cursor = cursor.AddMonths(-1);
            }

            return list;
        }

        public static (int Year, string Month) ResolvePeriod(int? year, string? month)
        {
            var now = DateTime.Now;
            var y = year ?? now.Year;
            var m = string.IsNullOrWhiteSpace(month)
                ? GetMonthName(now.Month)
                : month.Trim();

            return (y, m);
        }
    }
}
