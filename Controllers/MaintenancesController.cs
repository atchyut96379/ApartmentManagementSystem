using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Services;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize]
    public class MaintenancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardStatsService _dashboardStatsService;
        private readonly MaintenanceBillingService _billingService;

        public MaintenancesController(
            ApplicationDbContext context,
            DashboardStatsService dashboardStatsService,
            MaintenanceBillingService billingService)
        {
            _context = context;
            _dashboardStatsService = dashboardStatsService;
            _billingService = billingService;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

            ViewBag.DefaultAmount = _billingService.DefaultMonthlyAmount;
            ViewBag.FlatsWithoutMaintenance =
                await _dashboardStatsService.GetFlatsWithoutMaintenanceAsync();
            return View(await _context.Maintenances
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Id)
                .ToListAsync());
        }

        [Authorize(Roles = "Resident")]
        public IActionResult MyPayments()
        {
            return RedirectToAction("MyPayments", "ResidentPayments");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenance = await _context.Maintenances
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenance == null)
            {
                return NotFound();
            }

            return View(maintenance);
        }

        public IActionResult Create()
        {
            return ForbidAdminMaintenanceMutation();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            [Bind("Id,FlatNumber,Amount,Month,Year,DueDate,PaymentStatus,PaymentDate,Remarks")]
            Maintenance maintenance)
        {
            return ForbidAdminMaintenanceMutation();
        }

        public IActionResult Edit(int? id)
        {
            return ForbidAdminMaintenanceMutation();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            int? id,
            [Bind("Id,FlatNumber,Amount,Month,Year,DueDate,PaymentStatus,PaymentDate,Remarks")]
            Maintenance maintenance)
        {
            return ForbidAdminMaintenanceMutation();
        }

        public IActionResult Delete(int? id)
        {
            return ForbidAdminMaintenanceMutation();
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int? id)
        {
            return ForbidAdminMaintenanceMutation();
        }

        private IActionResult ForbidAdminMaintenanceMutation() => Forbid();
    }
}
