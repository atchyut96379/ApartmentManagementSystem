namespace ApartmentManagementSystem.Models
{
    public class ResidentImportResult
    {
        public int AddedCount { get; set; }

        public int SkippedCount { get; set; }

        public int TotalRowsProcessed { get; set; }

        public string SheetName { get; set; } = string.Empty;

        public string DiagnosticMessage { get; set; } = string.Empty;

        public List<ResidentImportRowResult> Rows { get; set; } = new();
    }

    public class ResidentImportRowResult
    {
        public int RowNumber { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string ResidentName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
