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

        // =========================
        // ADMIN - MAINTENANCE LIST
        // =========================

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

        // =========================
        // DETAILS
        // =========================

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

        // =========================
        // CREATE GET
        // =========================

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new Maintenance
            {
                PaymentStatus = false,
                DueDate = DateTime.Today,
                Year = DateTime.Today.Year,
                Remarks = string.Empty
            });
        }

        // =========================
        // CREATE POST
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [Bind("Id,FlatNumber,Amount,Month,Year,DueDate,PaymentStatus,PaymentDate,Remarks")]
            Maintenance maintenance)
        {
            if (ModelState.IsValid)
            {
                maintenance.FlatNumber = maintenance.FlatNumber.Trim();

                if (!maintenance.PaymentStatus)
                {
                    maintenance.PaymentDate = null;
                    maintenance.PaidDate = null;
                    maintenance.ReceiptNumber = null;
                }

                _context.Add(maintenance);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(maintenance);
        }

        // =========================
        // EDIT GET
        // =========================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenance =
                await _context.Maintenances.FindAsync(id);

            if (maintenance == null)
            {
                return NotFound();
            }

            return View(maintenance);
        }

        // =========================
        // EDIT POST
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,FlatNumber,Amount,Month,Year,DueDate,PaymentStatus,PaymentDate,Remarks")]
            Maintenance maintenance)
        {
            if (id != maintenance.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(maintenance);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MaintenanceExists(maintenance.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(maintenance);
        }

        // =========================
        // DELETE GET
        // =========================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
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

        // =========================
        // DELETE POST
        // =========================

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var maintenance =
                await _context.Maintenances.FindAsync(id);

            if (maintenance != null)
            {
                _context.Maintenances.Remove(maintenance);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // EXISTS
        // =========================

        private bool MaintenanceExists(int? id)
        {
            return _context.Maintenances.Any(e => e.Id == id);
        }

    }
}