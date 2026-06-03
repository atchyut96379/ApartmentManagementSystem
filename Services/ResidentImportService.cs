using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ApartmentManagementSystem.Services
{
    public class ResidentImportService
    {
        private static readonly string[] TemplateHeaders =
        {
            "FlatNumber",
            "ResidentName",
            "Email",
            "Phone",
            "ResidentType",
            "PropertyOwnerName"
        };

        private static readonly Regex FlatNumberPattern = new(
            @"^\d{1,4}(-\d{1,4})?$",
            RegexOptions.Compiled);

        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;

        public ResidentImportService(
            ApplicationDbContext context,
            MaintenanceBillingService billingService)
        {
            _context = context;
            _billingService = billingService;
        }

        public byte[] GenerateTemplate()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Residents");

            for (var i = 0; i < TemplateHeaders.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = TemplateHeaders[i];
            }

            sheet.Cell(2, 1).Value = "101";
            sheet.Cell(2, 2).Value = "Jane Doe";
            sheet.Cell(2, 3).Value = "jane@example.com";
            sheet.Cell(2, 4).Value = "9876543210";
            sheet.Cell(2, 5).Value = "Owner";
            sheet.Cell(2, 6).Value = "";

            sheet.Cell(3, 1).Value = "102";
            sheet.Cell(3, 2).Value = "John Tenant";
            sheet.Cell(3, 3).Value = "john@example.com";
            sheet.Cell(3, 4).Value = "9876543211";
            sheet.Cell(3, 5).Value = "Tenant";
            sheet.Cell(3, 6).Value = "Property Owner Name";

            sheet.Columns().AdjustToContents();

            var societySheet = workbook.Worksheets.Add("SocietyFormat");
            societySheet.Cell(1, 1).Value = "S.No";
            societySheet.Cell(1, 2).Value = "Name";
            societySheet.Cell(1, 3).Value = "Flat No";
            societySheet.Range(1, 4, 1, 5).Merge();
            societySheet.Cell(1, 4).Value = "Resident Type";
            societySheet.Cell(1, 6).Value = "Owner Contact Number";
            societySheet.Cell(1, 7).Value = "Tenant Contact number";

            societySheet.Cell(2, 4).Value = "Owner";
            societySheet.Cell(2, 5).Value = "Tenant";

            societySheet.Cell(3, 1).Value = 1;
            societySheet.Cell(3, 2).Value = "Jane Doe";
            societySheet.Cell(3, 3).Value = "101";
            societySheet.Cell(3, 4).Value = "Owner";
            societySheet.Cell(3, 5).Value = "";
            societySheet.Cell(3, 6).Value = "9876543210";
            societySheet.Cell(3, 7).Value = "";

            societySheet.Cell(4, 1).Value = 2;
            societySheet.Cell(4, 2).Value = "John Tenant";
            societySheet.Cell(4, 3).Value = "102";
            societySheet.Cell(4, 4).Value = "";
            societySheet.Cell(4, 5).Value = "Tenant";
            societySheet.Cell(4, 6).Value = "9123456789";
            societySheet.Cell(4, 7).Value = "9876543211 / 9876543212";

            societySheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<ResidentImportResult> ImportFromStreamAsync(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = SelectImportWorksheet(workbook);

            if (IsSocietyFormat(sheet))
            {
                return await ImportSocietyFormatAsync(sheet);
            }

            return await ImportTemplateFormatAsync(sheet);
        }

        private static IXLWorksheet SelectImportWorksheet(XLWorkbook workbook)
        {
            var societyFormat = workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Equals("SocietyFormat", StringComparison.OrdinalIgnoreCase));
            if (societyFormat != null)
            {
                return societyFormat;
            }

            foreach (var worksheet in workbook.Worksheets)
            {
                var map = DetectSocietyColumns(worksheet);
                if (IsValidSocietyColumnMap(map))
                {
                    return worksheet;
                }
            }

            return workbook.Worksheets
                .OrderByDescending(w => w.LastRowUsed()?.RowNumber() ?? 0)
                .First();
        }

        private static bool IsValidSocietyColumnMap(SocietyColumnMap? map) =>
            map != null && map.NameCol > 0 && map.FlatCol > 0;

        private async Task<ResidentImportResult> ImportTemplateFormatAsync(IXLWorksheet sheet)
        {
            var result = new ResidentImportResult();
            var existingResidents = await _context.Residents.ToListAsync();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var flatNumber = GetCellText(sheet, rowNumber, 1);
                var residentName = GetCellText(sheet, rowNumber, 2);
                var email = GetCellText(sheet, rowNumber, 3);
                var phone = GetCellText(sheet, rowNumber, 4);
                var residentType = GetCellText(sheet, rowNumber, 5);
                var propertyOwnerName = GetCellText(sheet, rowNumber, 6);

                if (IsRowEmpty(flatNumber, residentName, email, phone, residentType, propertyOwnerName))
                {
                    continue;
                }

                flatNumber = FlatNumberHelper.NormalizeSocietyFlatNumber(flatNumber);
                await ProcessImportRowAsync(
                    result,
                    existingResidents,
                    rowNumber,
                    flatNumber,
                    residentName,
                    email,
                    phone,
                    residentType,
                    propertyOwnerName,
                    ownerContactNumber: null);
            }

            await SaveImportResultAsync(result);
            return result;
        }

        private async Task<ResidentImportResult> ImportSocietyFormatAsync(IXLWorksheet sheet)
        {
            var result = new ResidentImportResult();
            var existingResidents = await _context.Residents.ToListAsync();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

            var columnMap = DetectSocietyColumns(sheet);
            if (columnMap == null)
            {
                result.SkippedCount = 1;
                result.Rows.Add(new ResidentImportRowResult
                {
                    RowNumber = 1,
                    Status = "Skipped",
                    Message = "Could not detect society format column headers (Name, Flat No, Owner/Tenant)."
                });
                return result;
            }

            var startRow = FindSocietyDataStartRow(sheet, lastRow, columnMap);
            TryMapTypeSubHeaders(sheet, columnMap.HeaderRow + 1, sheet.LastColumnUsed()?.ColumnNumber() ?? 10, columnMap, overwrite: true);

            for (var rowNumber = startRow; rowNumber <= lastRow; rowNumber++)
            {
                if (IsSocietySubHeaderRow(sheet, rowNumber, columnMap))
                {
                    continue;
                }

                var residentName = columnMap.NameCol > 0
                    ? GetCellText(sheet, rowNumber, columnMap.NameCol)
                    : string.Empty;
                var flatNumber = columnMap.FlatCol > 0
                    ? GetCellText(sheet, rowNumber, columnMap.FlatCol)
                    : string.Empty;

                if (LooksLikeFlatNumber(residentName) && LooksLikeName(flatNumber))
                {
                    (residentName, flatNumber) = (flatNumber, residentName);
                }

                if (string.IsNullOrWhiteSpace(residentName) && string.IsNullOrWhiteSpace(flatNumber))
                {
                    continue;
                }

                if (IsRepeatedHeaderRow(residentName, flatNumber))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(residentName) && !string.IsNullOrWhiteSpace(flatNumber))
                {
                    residentName = TryAdjacentNameColumn(sheet, rowNumber, columnMap);
                }

                if (string.IsNullOrWhiteSpace(residentName) || string.IsNullOrWhiteSpace(flatNumber))
                {
                    if (!string.IsNullOrWhiteSpace(flatNumber))
                    {
                        result.Rows.Add(new ResidentImportRowResult
                        {
                            RowNumber = rowNumber,
                            FlatNumber = flatNumber,
                            Status = "Skipped",
                            Message = "Resident name is required (checked Name column and columns next to Flat No)."
                        });
                        result.SkippedCount++;
                    }
                    else if (!string.IsNullOrWhiteSpace(residentName))
                    {
                        result.Rows.Add(new ResidentImportRowResult
                        {
                            RowNumber = rowNumber,
                            Status = "Skipped",
                            Message = "Flat number is required."
                        });
                        result.SkippedCount++;
                    }

                    continue;
                }

                if (ShouldSkipMisplacedSerialRow(sheet, rowNumber, columnMap, flatNumber))
                {
                    continue;
                }

                flatNumber = FlatNumberHelper.NormalizeSocietyFlatNumber(flatNumber);

                var ownerMarker = GetSocietyTypeMarker(sheet, rowNumber, columnMap.OwnerCol);
                var tenantMarker = GetSocietyTypeMarker(sheet, rowNumber, columnMap.TenantCol);
                var ownerContact = columnMap.OwnerContactCol > 0
                    ? GetCellText(sheet, rowNumber, columnMap.OwnerContactCol)
                    : string.Empty;
                var tenantContact = columnMap.TenantContactCol > 0
                    ? GetCellText(sheet, rowNumber, columnMap.TenantContactCol)
                    : string.Empty;

                var isOwner = ParseSocietyResidentType(
                    sheet,
                    rowNumber,
                    columnMap,
                    ownerMarker,
                    tenantMarker,
                    ownerContact,
                    tenantContact);
                if (isOwner == null)
                {
                    result.Rows.Add(new ResidentImportRowResult
                    {
                        RowNumber = rowNumber,
                        FlatNumber = flatNumber,
                        Status = "Skipped",
                        Message = "Row must indicate Owner or Tenant in the type columns."
                    });
                    result.SkippedCount++;
                    continue;
                }

                var phone = isOwner.Value
                    ? ExtractFirstPhone(ownerContact)
                    : ExtractFirstPhone(tenantContact);

                string? propertyOwnerName = null;
                string? ownerContactNumber = null;
                if (!isOwner.Value)
                {
                    ownerContactNumber = ExtractFirstPhone(ownerContact);
                    if (string.IsNullOrWhiteSpace(ownerContactNumber))
                    {
                        ownerContactNumber = null;
                    }
                }

                await ProcessImportRowAsync(
                    result,
                    existingResidents,
                    rowNumber,
                    flatNumber,
                    residentName,
                    email: string.Empty,
                    phone,
                    isOwner.Value ? "Owner" : "Tenant",
                    propertyOwnerName,
                    ownerContactNumber);
            }

            if (result.AddedCount + result.SkippedCount == 0)
            {
                result.Rows.Add(new ResidentImportRowResult
                {
                    RowNumber = 0,
                    Status = "Skipped",
                    Message = "No data rows found. Put residents on SocietyFormat sheet or use first sheet with Name+Flat No columns."
                });
                result.SkippedCount = 1;
            }

            await SaveImportResultAsync(result);
            return result;
        }

        private sealed class SocietyColumnMap
        {
            public int HeaderRow { get; set; }
            public int SerialCol { get; set; }
            public int NameCol { get; set; }
            public int FlatCol { get; set; }
            public int OwnerCol { get; set; }
            public int TenantCol { get; set; }
            public int OwnerContactCol { get; set; }
            public int TenantContactCol { get; set; }
        }

        private static SocietyColumnMap? DetectSocietyColumns(IXLWorksheet sheet)
        {
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;
            SocietyColumnMap? best = null;
            var bestScore = 0;

            for (var row = 1; row <= 8; row++)
            {
                var map = TryMapColumnsFromRow(sheet, row, lastCol);
                if (map.NameCol == 0 && map.FlatCol == 0)
                {
                    continue;
                }

                if (map.NameCol > 0 || map.FlatCol > 0)
                {
                    TryMapTypeSubHeaders(sheet, row + 1, lastCol, map, overwrite: true);
                }

                var score = ScoreColumnMap(map);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = map;
                    best.HeaderRow = row;
                }
            }

            return bestScore >= 2 ? best : null;
        }

        private static SocietyColumnMap TryMapColumnsFromRow(IXLWorksheet sheet, int row, int lastCol)
        {
            var map = new SocietyColumnMap { HeaderRow = row };

            for (var col = 1; col <= lastCol; col++)
            {
                var text = NormalizeHeaderText(GetCellText(sheet, row, col));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (IsSerialHeader(text) && map.SerialCol == 0)
                {
                    map.SerialCol = col;
                }
                else if (IsOwnerContactHeader(text) && map.OwnerContactCol == 0)
                {
                    map.OwnerContactCol = col;
                }
                else if (IsTenantContactHeader(text) && map.TenantContactCol == 0)
                {
                    map.TenantContactCol = col;
                }
                else if (IsFlatHeader(text) && map.FlatCol == 0)
                {
                    map.FlatCol = col;
                }
                else if (IsNameHeader(text) && map.NameCol == 0 && !IsSerialHeader(text))
                {
                    map.NameCol = col;
                }
                else if (IsResidentTypeHeader(text))
                {
                    if (map.OwnerCol == 0)
                    {
                        map.OwnerCol = col;
                    }

                    if (map.TenantCol == 0 && col + 1 <= lastCol)
                    {
                        map.TenantCol = col + 1;
                    }
                }
                else if (IsTenantHeader(text) && map.TenantCol == 0)
                {
                    map.TenantCol = col;
                }
                else if (IsOwnerHeader(text) && map.OwnerCol == 0)
                {
                    map.OwnerCol = col;
                }
            }

            return map;
        }

        private static void TryMapTypeSubHeaders(
            IXLWorksheet sheet,
            int row,
            int lastCol,
            SocietyColumnMap map,
            bool overwrite = false)
        {
            if (row < 1 || row > sheet.LastRowUsed()?.RowNumber())
            {
                return;
            }

            for (var col = 1; col <= lastCol; col++)
            {
                var text = NormalizeHeaderText(GetCellText(sheet, row, col));
                if (text == "owner" && (overwrite || map.OwnerCol == 0))
                {
                    map.OwnerCol = col;
                }
                else if (text == "tenant" && (overwrite || map.TenantCol == 0))
                {
                    map.TenantCol = col;
                }
            }
        }

        private static int ScoreColumnMap(SocietyColumnMap map)
        {
            var score = 0;
            if (map.NameCol > 0) score += 2;
            if (map.FlatCol > 0) score += 2;
            if (map.OwnerCol > 0) score += 1;
            if (map.TenantCol > 0) score += 1;
            if (map.OwnerContactCol > 0) score += 1;
            if (map.TenantContactCol > 0) score += 1;

            if (map.SerialCol == 1 &&
                map.NameCol == 2 &&
                map.FlatCol == 3 &&
                map.OwnerCol == 4 &&
                map.TenantCol == 5 &&
                map.OwnerContactCol == 6 &&
                map.TenantContactCol == 7)
            {
                score += 6;
            }

            return score;
        }

        private static int FindSocietyDataStartRow(IXLWorksheet sheet, int lastRow, SocietyColumnMap map)
        {
            var start = map.HeaderRow + 1;

            if (start <= lastRow && IsSocietySubHeaderRow(sheet, start, map))
            {
                start++;
            }

            for (var row = start; row <= lastRow; row++)
            {
                if (IsSocietySubHeaderRow(sheet, row, map))
                {
                    continue;
                }

                var name = map.NameCol > 0 ? GetCellText(sheet, row, map.NameCol) : string.Empty;
                var flat = map.FlatCol > 0 ? GetCellText(sheet, row, map.FlatCol) : string.Empty;

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(flat))
                {
                    continue;
                }

                if (IsRepeatedHeaderRow(name, flat))
                {
                    continue;
                }

                return row;
            }

            return start;
        }

        private static bool IsSocietySubHeaderRow(IXLWorksheet sheet, int row, SocietyColumnMap map)
        {
            var name = map.NameCol > 0 ? GetCellText(sheet, row, map.NameCol) : string.Empty;
            var flat = map.FlatCol > 0 ? GetCellText(sheet, row, map.FlatCol) : string.Empty;

            if (!string.IsNullOrWhiteSpace(name) && LooksLikeName(name) &&
                !string.IsNullOrWhiteSpace(flat) && LooksLikeFlatNumber(flat))
            {
                return false;
            }

            var ownerText = map.OwnerCol > 0 ? GetCellText(sheet, row, map.OwnerCol) : string.Empty;
            var tenantText = map.TenantCol > 0 ? GetCellText(sheet, row, map.TenantCol) : string.Empty;

            return ownerText.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                   tenantText.Equals("Tenant", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRepeatedHeaderRow(string name, string flat)
        {
            var normalizedName = NormalizeHeaderText(name);
            var normalizedFlat = NormalizeHeaderText(flat);
            return IsNameHeader(normalizedName) || IsFlatHeader(normalizedFlat);
        }

        private static bool? ParseSocietyResidentType(
            IXLWorksheet sheet,
            int row,
            SocietyColumnMap map,
            string ownerMarker,
            string tenantMarker,
            string ownerContact,
            string tenantContact)
        {
            if (CellIndicatesTenant(tenantMarker))
            {
                return false;
            }

            if (CellIndicatesOwner(ownerMarker))
            {
                return true;
            }

            if (map.OwnerCol > 0)
            {
                var ownerCell = GetCellText(sheet, row, map.OwnerCol);
                if (CellIndicatesTenant(ownerCell))
                {
                    return false;
                }

                if (CellIndicatesOwner(ownerCell))
                {
                    return true;
                }
            }

            if (map.TenantCol > 0)
            {
                var tenantCell = GetCellText(sheet, row, map.TenantCol);
                if (CellIndicatesTenant(tenantCell))
                {
                    return false;
                }

                if (CellIndicatesOwner(tenantCell))
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(ownerMarker) &&
                HasPhoneDigits(tenantContact) &&
                (CellIndicatesTenant(tenantMarker) || IsPositiveTypeMarker(tenantMarker)))
            {
                return false;
            }

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;
            for (var col = 1; col <= lastCol; col++)
            {
                if (col == map.SerialCol || col == map.NameCol || col == map.FlatCol ||
                    col == map.OwnerContactCol || col == map.TenantContactCol)
                {
                    continue;
                }

                var text = GetCellText(sheet, row, col);
                if (CellIndicatesTenant(text))
                {
                    return false;
                }
            }

            for (var col = 1; col <= lastCol; col++)
            {
                if (col == map.SerialCol || col == map.NameCol || col == map.FlatCol ||
                    col == map.OwnerContactCol || col == map.TenantContactCol)
                {
                    continue;
                }

                var text = GetCellText(sheet, row, col);
                if (CellIndicatesOwner(text))
                {
                    return true;
                }
            }

            if (TypeColumnsBlank(ownerMarker, tenantMarker, map, sheet, row))
            {
                return InferTypeFromContacts(ownerContact, tenantContact);
            }

            if (!string.IsNullOrWhiteSpace(ownerMarker) && string.IsNullOrWhiteSpace(tenantMarker))
            {
                var parsed = ParseResidentType(ownerMarker);
                if (parsed != null)
                {
                    return parsed;
                }
            }

            if (!string.IsNullOrWhiteSpace(tenantMarker) && string.IsNullOrWhiteSpace(ownerMarker))
            {
                var parsed = ParseResidentType(tenantMarker);
                if (parsed != null)
                {
                    return parsed;
                }
            }

            return InferTypeFromContacts(ownerContact, tenantContact);
        }

        private static bool TypeColumnsBlank(
            string ownerMarker,
            string tenantMarker,
            SocietyColumnMap map,
            IXLWorksheet sheet,
            int row)
        {
            if (!string.IsNullOrWhiteSpace(ownerMarker) || !string.IsNullOrWhiteSpace(tenantMarker))
            {
                return false;
            }

            if (map.OwnerCol > 0 && !string.IsNullOrWhiteSpace(GetCellText(sheet, row, map.OwnerCol)))
            {
                return false;
            }

            if (map.TenantCol > 0 && !string.IsNullOrWhiteSpace(GetCellText(sheet, row, map.TenantCol)))
            {
                return false;
            }

            return true;
        }

        private static bool? InferTypeFromContacts(string ownerContact, string tenantContact)
        {
            var hasOwnerPhone = HasPhoneDigits(ownerContact);
            var hasTenantPhone = HasPhoneDigits(tenantContact);

            if (hasTenantPhone && !hasOwnerPhone)
            {
                return false;
            }

            if (hasOwnerPhone && !hasTenantPhone)
            {
                return true;
            }

            if (hasTenantPhone)
            {
                return false;
            }

            return null;
        }

        private static bool CellIndicatesOwner(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (ContainsTenantWord(text))
            {
                return false;
            }

            if (ContainsOwnerWord(text))
            {
                return true;
            }

            return IsPositiveTypeMarker(text);
        }

        private static bool CellIndicatesTenant(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (ContainsOwnerWord(text) && !ContainsTenantWord(text))
            {
                return false;
            }

            if (ContainsTenantWord(text))
            {
                return true;
            }

            return IsPositiveTypeMarker(text);
        }

        private static bool IsPositiveTypeMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed.Equals("x", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                trimmed == "1")
            {
                return true;
            }

            return IsCheckmark(trimmed);
        }

        private static bool IsCheckmark(string text) =>
            text is "✓" or "✔" or "☑" or "✅" or "√";

        private static bool ContainsOwnerWord(string text) =>
            !string.IsNullOrWhiteSpace(text) &&
            text.Contains("Owner", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("Tenant", StringComparison.OrdinalIgnoreCase);

        private static bool ContainsTenantWord(string text) =>
            !string.IsNullOrWhiteSpace(text) &&
            text.Contains("Tenant", StringComparison.OrdinalIgnoreCase);

        private static bool HasPhoneDigits(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text.Count(char.IsDigit) >= 6;

        private static bool ShouldSkipMisplacedSerialRow(
            IXLWorksheet sheet,
            int rowNumber,
            SocietyColumnMap map,
            string flatNumber)
        {
            if (string.IsNullOrWhiteSpace(flatNumber) || map.SerialCol <= 0)
            {
                return false;
            }

            var trimmed = flatNumber.Trim();
            var serial = GetCellText(sheet, rowNumber, map.SerialCol).Trim();
            return !string.IsNullOrEmpty(serial) &&
                   trimmed.Equals(serial, StringComparison.OrdinalIgnoreCase);
        }

        private static string TryAdjacentNameColumn(IXLWorksheet sheet, int row, SocietyColumnMap map)
        {
            if (map.FlatCol <= 0)
            {
                return string.Empty;
            }

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;
            foreach (var col in new[] { map.FlatCol + 1, map.FlatCol - 1 })
            {
                if (col < 1 || col > lastCol || col == map.FlatCol || col == map.SerialCol)
                {
                    continue;
                }

                var text = GetCellText(sheet, row, col);
                if (!string.IsNullOrWhiteSpace(text) &&
                    LooksLikeName(text) &&
                    !LooksLikeFlatNumber(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string GetSocietyTypeMarker(IXLWorksheet sheet, int row, int col)
        {
            if (col <= 0)
            {
                return string.Empty;
            }

            var text = GetCellText(sheet, row, col);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;
            if (col + 1 <= lastCol)
            {
                return GetCellText(sheet, row, col + 1);
            }

            return string.Empty;
        }

        private static bool LooksLikeFlatNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (FlatNumberPattern.IsMatch(trimmed))
            {
                return true;
            }

            return trimmed.All(c => char.IsDigit(c) || c == '-' || c == ' ') &&
                   trimmed.Any(char.IsDigit);
        }

        private static bool LooksLikeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (LooksLikeFlatNumber(value))
            {
                return false;
            }

            return value.Trim().Any(char.IsLetter);
        }

        private static string NormalizeHeaderText(string text)
        {
            return text.Trim()
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Replace("  ", " ")
                .ToLowerInvariant();
        }

        private static bool IsSerialHeader(string text)
        {
            return text is "s.no" or "s no" or "s.no." or "s no." or "serial" or "serial no" or "serial no." or
                   "sl no" or "sl.no" or "sl no." ||
                   text.StartsWith("s.no", StringComparison.Ordinal);
        }

        private static bool IsNameHeader(string text) =>
            !IsSerialHeader(text) &&
            (text is "name" or "resident name" or "member name");

        private static bool IsFlatHeader(string text) =>
            text.Contains("flat") ||
            text is "flat no" or "flat no." or "flat number" or "flat #";

        private static bool IsOwnerContactHeader(string text) =>
            text.Contains("owner contact") || text.Contains("owner phone") || text.Contains("owner mobile");

        private static bool IsTenantContactHeader(string text) =>
            text.Contains("tenant contact") || text.Contains("tenant phone") || text.Contains("tenant mobile");

        private static bool IsOwnerHeader(string text) =>
            text == "owner" ||
            (text.Contains("owner") &&
             !text.Contains("contact") &&
             !text.Contains("phone") &&
             !text.Contains("mobile") &&
             !text.Contains("number"));

        private static bool IsTenantHeader(string text) =>
            text == "tenant" ||
            (text.Contains("tenant") &&
             !text.Contains("contact") &&
             !text.Contains("phone") &&
             !text.Contains("mobile") &&
             !text.Contains("number"));

        private static bool IsResidentTypeHeader(string text) =>
            text.Contains("resident type") || text.Contains("type of resident");

        private async Task ProcessImportRowAsync(
            ResidentImportResult result,
            List<Resident> existingResidents,
            int rowNumber,
            string flatNumber,
            string residentName,
            string email,
            string? phone,
            string residentType,
            string? propertyOwnerName,
            string? ownerContactNumber)
        {
            var rowResult = new ResidentImportRowResult
            {
                RowNumber = rowNumber,
                FlatNumber = flatNumber
            };

            if (string.IsNullOrWhiteSpace(flatNumber))
            {
                rowResult.Status = "Skipped";
                rowResult.Message = "Flat number is required.";
                result.Rows.Add(rowResult);
                result.SkippedCount++;
                return;
            }

            if (string.IsNullOrWhiteSpace(residentName))
            {
                rowResult.Status = "Skipped";
                rowResult.Message = "Resident name is required.";
                result.Rows.Add(rowResult);
                result.SkippedCount++;
                return;
            }

            var isOwner = ParseResidentType(residentType);
            if (isOwner == null)
            {
                rowResult.Status = "Skipped";
                rowResult.Message = "Resident type must be Owner or Tenant.";
                result.Rows.Add(rowResult);
                result.SkippedCount++;
                return;
            }

            if (!isOwner.Value && !HasTenantOwnerInfo(propertyOwnerName, ownerContactNumber))
            {
                rowResult.Status = "Skipped";
                rowResult.Message = "Tenants require property owner name or owner contact number.";
                result.Rows.Add(rowResult);
                result.SkippedCount++;
                return;
            }

            flatNumber = flatNumber.Trim();

            if (IsDuplicate(existingResidents, flatNumber, email))
            {
                rowResult.Status = "Skipped";
                rowResult.Message = "Duplicate resident for this flat.";
                result.Rows.Add(rowResult);
                result.SkippedCount++;
                return;
            }

            var resident = new Resident
            {
                FlatNumber = flatNumber,
                OwnerName = residentName.Trim(),
                Email = email?.Trim() ?? string.Empty,
                PhoneNumber = phone?.Trim() ?? string.Empty,
                IsOwner = isOwner.Value,
                PropertyOwnerName = isOwner.Value
                    ? null
                    : string.IsNullOrWhiteSpace(propertyOwnerName) ? null : propertyOwnerName.Trim(),
                OwnerContactNumber = isOwner.Value
                    ? null
                    : ownerContactNumber?.Trim(),
                CreatedDate = DateTime.Now
            };

            _context.Residents.Add(resident);
            existingResidents.Add(resident);

            rowResult.Status = "Added";
            rowResult.Message = "Resident imported successfully.";
            result.Rows.Add(rowResult);
            result.AddedCount++;
        }

        private async Task SaveImportResultAsync(ResidentImportResult result)
        {
            if (result.AddedCount > 0)
            {
                await _context.SaveChangesAsync();
                await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();
            }
        }

        private static bool IsSocietyFormat(IXLWorksheet sheet)
        {
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;

            for (var row = 1; row <= 8; row++)
            {
                var headerText = string.Join(
                    " ",
                    Enumerable.Range(1, lastCol)
                        .Select(c => GetCellText(sheet, row, c)));

                if (headerText.Contains("Flat No", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("S.No", StringComparison.OrdinalIgnoreCase) ||
                    headerText.Contains("Resident Type", StringComparison.OrdinalIgnoreCase) ||
                    (headerText.Contains("Name", StringComparison.OrdinalIgnoreCase) &&
                     headerText.Contains("Flat", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractFirstPhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var parts = raw.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : raw.Trim();
        }

        private static bool HasTenantOwnerInfo(string? propertyOwnerName, string? ownerContactNumber)
        {
            return !string.IsNullOrWhiteSpace(propertyOwnerName) ||
                   !string.IsNullOrWhiteSpace(ownerContactNumber);
        }

        private static bool? ParseResidentType(string? residentType)
        {
            if (string.IsNullOrWhiteSpace(residentType))
            {
                return null;
            }

            var normalized = residentType.Trim();
            if (normalized.Equals("Owner", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Equals("Tenant", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }

        private static bool IsDuplicate(
            List<Resident> existingResidents,
            string flatNumber,
            string? email)
        {
            var normalizedEmail = NormalizeEmail(email);

            return existingResidents.Any(r =>
            {
                if (!FlatNumberHelper.Match(r.FlatNumber, flatNumber))
                {
                    return false;
                }

                var existingEmail = NormalizeEmail(r.Email);
                if (!string.IsNullOrEmpty(normalizedEmail) &&
                    !string.IsNullOrEmpty(existingEmail))
                {
                    return existingEmail == normalizedEmail;
                }

                return string.IsNullOrEmpty(normalizedEmail) &&
                       string.IsNullOrEmpty(existingEmail);
            });
        }

        private static string? NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? null
                : email.Trim().ToLowerInvariant();
        }

        private static string GetCellText(IXLWorksheet sheet, int row, int column)
        {
            var cell = sheet.Cell(row, column);
            if (cell.IsEmpty())
            {
                return string.Empty;
            }

            if (cell.DataType == XLDataType.Number)
            {
                var num = cell.GetDouble();
                if (num == Math.Floor(num) && !double.IsNaN(num) && !double.IsInfinity(num))
                {
                    return ((long)num).ToString();
                }

                return num.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var formatted = cell.GetFormattedString();
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                return formatted.Trim();
            }

            return cell.GetString().Trim();
        }

        private static bool IsRowEmpty(params string?[] values)
        {
            return values.All(string.IsNullOrWhiteSpace);
        }
    }
}
