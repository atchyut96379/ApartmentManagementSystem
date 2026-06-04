using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagementSystem.Services
{
    public class ResidentBulkLoginService
    {
        private readonly ApplicationDbContext _context;
        private readonly ResidentLoginProvisioningService _loginProvisioning;
        private readonly ResidentAccountService _accountService;
        private readonly LoginIdentityService _loginIdentity;
        private readonly IMemoryCache _cache;

        public ResidentBulkLoginService(
            ApplicationDbContext context,
            ResidentLoginProvisioningService loginProvisioning,
            ResidentAccountService accountService,
            LoginIdentityService loginIdentity,
            IMemoryCache cache)
        {
            _context = context;
            _loginProvisioning = loginProvisioning;
            _accountService = accountService;
            _loginIdentity = loginIdentity;
            _cache = cache;
        }

        public async Task<int> CountResidentsWithoutLoginAsync()
        {
            return await _context.Residents.CountAsync(r =>
                r.MemberType != ResidentMemberType.AssociationAdmin &&
                string.IsNullOrEmpty(r.UserId));
        }

        public async Task<int> CountResidentsWithoutLoginAndPhoneAsync()
        {
            var residents = await _context.Residents
                .Where(r =>
                    r.MemberType != ResidentMemberType.AssociationAdmin &&
                    string.IsNullOrEmpty(r.UserId))
                .ToListAsync();

            return residents.Count(r =>
                LoginIdentityService.IsValidLoginPhone(r.PhoneNumber));
        }

        public async Task<BulkLoginProvisioningResult> CreateLoginsAsync()
        {
            var residents = await _context.Residents
                .Where(r =>
                    r.MemberType != ResidentMemberType.AssociationAdmin &&
                    string.IsNullOrEmpty(r.UserId))
                .OrderBy(r => r.FlatNumber)
                .ThenBy(r => r.OwnerName)
                .ToListAsync();

            var result = new BulkLoginProvisioningResult
            {
                EligibleCount = residents.Count,
                PasswordFormatDescription = _accountService.DescribeDefaultPasswordFormat()
            };

            foreach (var resident in residents)
            {
                var row = new BulkLoginRowResult
                {
                    ResidentId = resident.Id,
                    FlatNumber = resident.FlatNumber,
                    ResidentName = resident.OwnerName
                };

                if (!LoginIdentityService.IsValidLoginPhone(resident.PhoneNumber))
                {
                    row.Status = "Skipped";
                    row.Message = "Add a valid mobile number (10+ digits) on the resident record.";
                    result.Rows.Add(row);
                    result.SkippedCount++;
                    continue;
                }

                var provision = await _loginProvisioning.CreateLoginForResidentAsync(resident);

                if (!provision.Succeeded)
                {
                    row.Status = "Skipped";
                    row.LoginPhone = LoginIdentityService.NormalizeLoginPhone(resident.PhoneNumber);
                    row.Message = string.Join(" ", provision.Errors);
                    result.Rows.Add(row);
                    result.SkippedCount++;
                    continue;
                }

                row.Status = "Created";
                row.LoginPhone = provision.LoginPhone ?? string.Empty;
                row.TemporaryPassword = provision.TemporaryPassword ?? string.Empty;
                row.Message = "Sign in with mobile number and temporary password; change password on first login.";
                result.Rows.Add(row);
                result.CreatedCount++;
            }

            if (result.CreatedCount > 0)
            {
                var bytes = BuildCredentialsWorkbook(
                    result.Rows.Where(r => r.Status == "Created").ToList());
                var downloadId = Guid.NewGuid().ToString("N");
                _cache.Set(
                    GetCacheKey(downloadId),
                    bytes,
                    TimeSpan.FromMinutes(30));
                result.SpreadsheetDownloadId = downloadId;
            }

            return result;
        }

        public byte[]? GetCachedSpreadsheet(string? downloadId)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return null;
            }

            return _cache.TryGetValue(GetCacheKey(downloadId), out byte[]? bytes) ? bytes : null;
        }

        private static string GetCacheKey(string downloadId) => $"bulk-login-export:{downloadId}";

        public static byte[] BuildCredentialsWorkbook(IReadOnlyList<BulkLoginRowResult> rows)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("ResidentLogins");
            sheet.Cell(1, 1).Value = "Flat";
            sheet.Cell(1, 2).Value = "Name";
            sheet.Cell(1, 3).Value = "Login mobile";
            sheet.Cell(1, 4).Value = "Temporary password";
            sheet.Cell(1, 5).Value = "Login URL";
            sheet.Row(1).Style.Font.Bold = true;

            var rowIndex = 2;
            foreach (var row in rows)
            {
                sheet.Cell(rowIndex, 1).Value = row.FlatNumber;
                sheet.Cell(rowIndex, 2).Value = row.ResidentName;
                sheet.Cell(rowIndex, 3).Value = row.LoginPhone;
                sheet.Cell(rowIndex, 4).Value = row.TemporaryPassword;
                sheet.Cell(rowIndex, 5).Value = "/Account/Login";
                rowIndex++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
