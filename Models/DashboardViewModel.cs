namespace ApartmentManagementSystem.Models
{
    public class DashboardViewModel
    {
        public int TotalResidents { get; set; }

        public int TotalFlats { get; set; }

        public int TotalOwners { get; set; }

        public int TotalTenants { get; set; }

        /// <summary>Sum of all paid maintenance records (all time).</summary>
        public decimal TotalCollected { get; set; }

        /// <summary>Paid maintenance for the current calendar month.</summary>
        public decimal CurrentMonthCollection { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal BalanceAmount { get; set; }

        public int PendingPayments { get; set; }

        public int TotalMaintenanceRecords { get; set; }

        public List<Maintenance> PendingMaintenanceList { get; set; } = new();

        public ResidentPaymentsViewModel? ResidentPayments { get; set; }
    }
}
