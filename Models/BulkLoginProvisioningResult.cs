namespace ApartmentManagementSystem.Models
{
    public class BulkLoginProvisioningResult
    {
        public int CreatedCount { get; set; }

        public int SkippedCount { get; set; }

        public int EligibleCount { get; set; }

        public int ResidentsWithValidPhone { get; set; }

        public string PasswordFormatDescription { get; set; } = string.Empty;

        public string? SpreadsheetDownloadId { get; set; }

        public List<BulkLoginRowResult> Rows { get; set; } = new();
    }

    public class BulkLoginRowResult
    {
        public int ResidentId { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string ResidentName { get; set; } = string.Empty;

        public string LoginPhone { get; set; } = string.Empty;

        public string TemporaryPassword { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public class BulkCreateLoginsViewModel
    {
        public int ResidentsWithoutLogin { get; set; }

        public int ResidentsWithValidPhone { get; set; }

        public string PasswordFormatDescription { get; set; } = string.Empty;
    }
}
