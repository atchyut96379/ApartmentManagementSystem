using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Services
{
    public class DashboardReportExportService
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardStatsService _statsService;

        public DashboardReportExportService(
            ApplicationDbContext context,
            DashboardStatsService statsService)
        {
            _context = context;
            _statsService = statsService;
        }

        public async Task<byte[]> BuildDashboardWorkbookAsync()
        {
            var stats = _statsService.GetSocietyStats();
            stats.PendingMaintenanceList =
                await _statsService.GetPendingMaintenancesAsync();

            var residents = await _context.Residents
                .OrderBy(r => r.FlatNumber)
                .ToListAsync();

            var maintenances = await _context.Maintenances
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Id)
                .ToListAsync();

            var expenses = await _context.Expenses
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            using var workbook = new XLWorkbook();

            AddSummarySheet(workbook, stats);
            AddResidentsSheet(workbook, residents);
            AddMaintenanceSheet(workbook, maintenances);
            AddExpensesSheet(workbook, expenses);
            AddPendingSheet(workbook, stats.PendingMaintenanceList);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void AddSummarySheet(XLWorkbook workbook, DashboardViewModel stats)
        {
            var sheet = workbook.Worksheets.Add("Summary");
            sheet.Cell(1, 1).Value = "Apartment Management — Dashboard Report";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd-MM-yyyy HH:mm}";

            var row = 4;
            WriteRow(sheet, ref row, "Total Residents", stats.TotalResidents);
            WriteRow(sheet, ref row, "Total Flats", stats.TotalFlats);
            WriteRow(sheet, ref row, "In-house Owners", stats.TotalOwners);
            WriteRow(sheet, ref row, "In-house Tenants", stats.TotalTenants);
            WriteRow(sheet, ref row, "Association committee", stats.TotalAssociationAdmins);
            WriteRow(sheet, ref row, "This month collected", stats.CurrentMonthCollection);
            WriteRow(sheet, ref row, "Total collected (paid)", stats.TotalCollected);
            WriteRow(sheet, ref row, "Total expenses", stats.TotalExpenses);
            WriteRow(sheet, ref row, "Balance", stats.BalanceAmount);
            WriteRow(sheet, ref row, "Pending payments", stats.PendingPayments);
            sheet.Columns().AdjustToContents();
        }

        private static void WriteRow(IXLWorksheet sheet, ref int row, string label, object value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            if (value is decimal d)
            {
                sheet.Cell(row, 2).Value = d;
                sheet.Cell(row, 2).Style.NumberFormat.Format = "₹#,##0.00";
            }
            else
            {
                sheet.Cell(row, 2).Value = value?.ToString() ?? "";
            }

            row++;
        }

        private static void AddResidentsSheet(XLWorkbook workbook, List<Resident> residents)
        {
            var sheet = workbook.Worksheets.Add("Residents");
            var headers = new[]
            {
                "Flat", "Name", "Phone", "Email", "Member Type", "Designation",
                "Property Owner", "Owner Contact", "Has Login"
            };

            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
                sheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var r in residents)
            {
                sheet.Cell(row, 1).Value = r.FlatNumber;
                sheet.Cell(row, 2).Value = r.OwnerName;
                sheet.Cell(row, 3).Value = r.PhoneNumber ?? "";
                sheet.Cell(row, 4).Value = r.Email ?? "";
                sheet.Cell(row, 5).Value = GetMemberTypeLabel(r);
                sheet.Cell(row, 6).Value = r.AssociationDesignation ?? "";
                sheet.Cell(row, 7).Value = r.PropertyOwnerName ?? "";
                sheet.Cell(row, 8).Value = r.OwnerContactNumber ?? "";
                sheet.Cell(row, 9).Value = string.IsNullOrEmpty(r.UserId) ? "No" : "Yes";
                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        private static string GetMemberTypeLabel(Resident r)
        {
            return r.MemberType switch
            {
                ResidentMemberType.Tenant => "Tenant",
                ResidentMemberType.AssociationAdmin => "Association Admin",
                _ => "Owner"
            };
        }

        private static void AddMaintenanceSheet(XLWorkbook workbook, List<Maintenance> items)
        {
            var sheet = workbook.Worksheets.Add("Maintenance");
            var headers = new[]
            {
                "Flat", "Amount", "Month", "Year", "Status", "Payment Date", "Remarks"
            };

            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
                sheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var m in items)
            {
                sheet.Cell(row, 1).Value = m.FlatNumber;
                sheet.Cell(row, 2).Value = m.Amount;
                sheet.Cell(row, 3).Value = m.Month;
                sheet.Cell(row, 4).Value = m.Year;
                sheet.Cell(row, 5).Value = m.PaymentStatus ? "Paid" : "Pending";
                sheet.Cell(row, 6).Value = m.PaymentDate?.ToString("dd-MM-yyyy") ?? "";
                sheet.Cell(row, 7).Value = m.Remarks ?? "";
                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        private static void AddExpensesSheet(XLWorkbook workbook, List<Expense> items)
        {
            var sheet = workbook.Worksheets.Add("Expenses");
            var headers = new[] { "Type", "Amount", "Date", "Description" };

            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
                sheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var e in items)
            {
                sheet.Cell(row, 1).Value = e.ExpenseType;
                sheet.Cell(row, 2).Value = e.Amount;
                sheet.Cell(row, 3).Value = e.ExpenseDate.ToString("dd-MM-yyyy HH:mm");
                sheet.Cell(row, 4).Value = e.Description;
                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        private static void AddPendingSheet(XLWorkbook workbook, List<Maintenance> pending)
        {
            var sheet = workbook.Worksheets.Add("Pending Dues");
            var headers = new[] { "Flat", "Month", "Year", "Amount" };

            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
                sheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var m in pending)
            {
                sheet.Cell(row, 1).Value = m.FlatNumber;
                sheet.Cell(row, 2).Value = m.Month;
                sheet.Cell(row, 3).Value = m.Year;
                sheet.Cell(row, 4).Value = m.Amount;
                row++;
            }

            sheet.Columns().AdjustToContents();
        }
    }
}
