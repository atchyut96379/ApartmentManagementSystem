namespace ApartmentManagementSystem.Models
{
    public class ResidentImportResult
    {
        public int AddedCount { get; set; }

        public int SkippedCount { get; set; }

        public List<ResidentImportRowResult> Rows { get; set; } = new();
    }

    public class ResidentImportRowResult
    {
        public int RowNumber { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
