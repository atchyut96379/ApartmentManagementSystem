using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Identity;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CommitteeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CommitteeAccessService _access;
        private readonly CommitteeLoginProvisioningService _committeeProvisioning;
        private readonly ResidentAccountService _accountService;

        public CommitteeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CommitteeAccessService access,
            CommitteeLoginProvisioningService committeeProvisioning,
            ResidentAccountService accountService)
        {
            _context = context;
            _userManager = userManager;
            _access = access;
            _committeeProvisioning = committeeProvisioning;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var deny = await DenyUnlessSystemAdminAsync();
            if (deny != null)
            {
                return deny;
            }

            var filled = await _context.Residents
                .Where(r => r.MemberType == ResidentMemberType.AssociationAdmin)
                .ToListAsync();

            var rows = AssociationDesignations.All.Select(d =>
            {
                var member = filled.FirstOrDefault(r =>
                    r.AssociationDesignation != null &&
                    r.AssociationDesignation.Equals(d, StringComparison.OrdinalIgnoreCase));

                return new CommitteeMemberRowViewModel
                {
                    Designation = d,
                    IsFilled = member != null,
                    ResidentId = member?.Id,
                    Name = member?.OwnerName,
                    FlatNumber = member?.FlatNumber,
                    LoginPhone = member?.PhoneNumber,
                    NotificationEmail = string.IsNullOrWhiteSpace(member?.Email) ? null : member.Email,
                    HasLogin = member != null && !string.IsNullOrEmpty(member.UserId)
                };
            }).ToList();

            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string? designation)
        {
            var deny = await DenyUnlessSystemAdminAsync();
            if (deny != null)
            {
                return deny;
            }

            if (string.IsNullOrWhiteSpace(designation) ||
                !AssociationDesignations.All.Contains(designation, StringComparer.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation, StringComparison.OrdinalIgnoreCase));

            var taken = await _context.Residents.AnyAsync(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin &&
                r.AssociationDesignation == designation);

            if (taken)
            {
                TempData["Error"] = $"{designation} already has an account.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();

            return View(new CreateCommitteeAccountViewModel
            {
                Designation = designation
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCommitteeAccountViewModel model)
        {
            var deny = await DenyUnlessSystemAdminAsync();
            if (deny != null)
            {
                return deny;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
                return View(model);
            }

            var result = await _committeeProvisioning.CreateCommitteeAccountAsync(
                model.Designation,
                model.OwnerName,
                model.FlatNumber,
                model.PhoneNumber,
                model.Email ?? string.Empty);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
                return View(model);
            }

            TempData["GeneratedLoginPhone"] = result.LoginPhone;
            TempData["GeneratedPassword"] = result.TemporaryPassword;
            TempData["GeneratedNotificationEmail"] = result.NotificationEmail;
            TempData["CredentialsIsReset"] = false;
            TempData["Success"] =
                $"{model.Designation} account created with admin access. Share mobile login and password; they can add residents after login.";

            var residentId = await _context.Residents
                .Where(r =>
                    r.MemberType == ResidentMemberType.AssociationAdmin &&
                    r.AssociationDesignation == model.Designation)
                .Select(r => r.Id)
                .FirstAsync();

            return RedirectToAction("LoginCredentials", "Residents", new { id = residentId });
        }

        [HttpGet]
        public async Task<IActionResult> AssignFromResident(string? designation)
        {
            var deny = await DenyUnlessSystemAdminAsync();
            if (deny != null)
            {
                return deny;
            }

            if (string.IsNullOrWhiteSpace(designation) ||
                !AssociationDesignations.All.Contains(designation, StringComparer.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            designation = AssociationDesignations.All.First(d =>
                d.Equals(designation, StringComparison.OrdinalIgnoreCase));

            var taken = await _context.Residents.AnyAsync(r =>
                r.MemberType == ResidentMemberType.AssociationAdmin &&
                r.AssociationDesignation == designation);

            if (taken)
            {
                TempData["Error"] = $"{designation} is already assigned.";
                return RedirectToAction(nameof(Index));
            }

            var candidates = await _context.Residents
                .Where(r => r.MemberType != ResidentMemberType.AssociationAdmin)
                .OrderBy(r => r.FlatNumber)
                .ThenBy(r => r.OwnerName)
                .Select(r => new ResidentPickItem
                {
                    Id = r.Id,
                    FlatNumber = r.FlatNumber,
                    OwnerName = r.OwnerName,
                    PhoneNumber = r.PhoneNumber,
                    Email = r.Email,
                    HasLogin = !string.IsNullOrEmpty(r.UserId),
                    MemberTypeLabel = r.MemberType == ResidentMemberType.Tenant ? "Tenant" : "Owner"
                })
                .ToListAsync();

            if (candidates.Count == 0)
            {
                TempData["Error"] = "No residents in the list. Import residents from Excel first.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();

            return View(new AssignCommitteeFromResidentViewModel
            {
                Designation = designation,
                Candidates = candidates
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignFromResident(AssignCommitteeFromResidentViewModel model)
        {
            var deny = await DenyUnlessSystemAdminAsync();
            if (deny != null)
            {
                return deny;
            }

            if (!ModelState.IsValid)
            {
                model.Candidates = await LoadCandidatesAsync();
                ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
                return View(model);
            }

            var result = await _committeeProvisioning.PromoteResidentToCommitteeAsync(
                model.ResidentId,
                model.Designation,
                model.NotificationEmail);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                model.Candidates = await LoadCandidatesAsync();
                ViewBag.DefaultPasswordFormat = _accountService.DescribeDefaultPasswordFormat();
                return View(model);
            }

            TempData["GeneratedLoginPhone"] = result.LoginPhone;
            TempData["GeneratedPassword"] = result.TemporaryPassword;
            TempData["GeneratedNotificationEmail"] = result.NotificationEmail;
            TempData["CredentialsIsReset"] = false;
            TempData["Success"] =
                $"{model.Designation} assigned from your resident list with admin access.";

            return RedirectToAction("LoginCredentials", "Residents", new { id = model.ResidentId });
        }

        private async Task<List<ResidentPickItem>> LoadCandidatesAsync()
        {
            return await _context.Residents
                .Where(r => r.MemberType != ResidentMemberType.AssociationAdmin)
                .OrderBy(r => r.FlatNumber)
                .ThenBy(r => r.OwnerName)
                .Select(r => new ResidentPickItem
                {
                    Id = r.Id,
                    FlatNumber = r.FlatNumber,
                    OwnerName = r.OwnerName,
                    PhoneNumber = r.PhoneNumber,
                    Email = r.Email,
                    HasLogin = !string.IsNullOrEmpty(r.UserId),
                    MemberTypeLabel = r.MemberType == ResidentMemberType.Tenant ? "Tenant" : "Owner"
                })
                .ToListAsync();
        }

        private async Task<IActionResult?> DenyUnlessSystemAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (!_access.IsSystemAdmin(user))
            {
                TempData["Error"] =
                    "Only the system administrator (Admin) can manage committee accounts.";
                return RedirectToAction("Index", "Home");
            }

            return null;
        }
    }
}
