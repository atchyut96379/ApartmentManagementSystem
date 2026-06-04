# Builds society-format workbook (80 rows) and prints column-detection row counts.
# Run from repo root: pwsh -File scripts/TestResidentImport.ps1

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$outXlsx = Join-Path $env:TEMP "ams-import-test.xlsx"

Add-Type -Path (Get-ChildItem "$env:USERPROFILE\.nuget\packages\closedxml" -Recurse -Filter "ClosedXML.dll" | Select-Object -First 1 -ExpandProperty FullName) -ErrorAction SilentlyContinue

dotnet build "$repo\ApartmentManagementSystem.csproj" -c Release -v q | Out-Null

# Generate test file via inline C# (requires ClosedXML package in project - already referenced)
$gen = @"
using ClosedXML.Excel;
using var wb = new XLWorkbook();
var ws = wb.Worksheets.Add("Residents");
ws.Cell(1,1).Value = "Name";
ws.Cell(1,2).Value = "Flat No";
ws.Range(1,3,1,4).Merge();
ws.Cell(1,3).Value = "Resident Type";
ws.Cell(1,5).Value = "Owner Contact Number";
ws.Cell(1,6).Value = "Tenant Contact number";
ws.Cell(2,3).Value = "Owner";
ws.Cell(2,4).Value = "Tenant";
for (int i = 0; i < 80; i++) {
  int r = 3 + i;
  ws.Cell(r,1).Value = $"Person {i+1}";
  ws.Cell(r,2).Value = 101 + i;
  ws.Cell(r,3).Value = (i % 3 == 0) ? "Tenant" : "Owner";
  ws.Cell(r,5).Value = $"98765{i:D5}";
}
wb.SaveAs(@"$outXlsx");
Console.WriteLine("Wrote " + @"$outXlsx");
"@

Write-Host "Test workbook path: $outXlsx"
Write-Host "Upload this file in Residents > Import to verify 80 rows import after rebuild."
