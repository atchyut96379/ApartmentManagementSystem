using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Services;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ResidentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceBillingService _billingService;

        public ResidentsController(
            ApplicationDbContext context,
            MaintenanceBillingService billingService)
        {
            _context = context;
            _billingService = billingService;
        }

        // =========================
        // RESIDENT LIST
        // =========================

        public async Task<IActionResult> Index()
        {
            return View(await _context.Residents.ToListAsync());
        }

        // =========================
        // DETAILS
        // =========================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents
                .FirstOrDefaultAsync(m => m.Id == id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        // =========================
        // CREATE GET
        // =========================

        public IActionResult Create()
        {
            return View();
        }

        // =========================
        // CREATE POST
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Id,FlatNumber,OwnerName,PhoneNumber,Email,IsOwner,CreatedDate")]
            Resident resident)
        {
            if (ModelState.IsValid)
            {
                resident.FlatNumber = resident.FlatNumber.Trim();
                _context.Add(resident);
                await _context.SaveChangesAsync();
                await _billingService.EnsureMonthlyMaintenanceForAllResidentsAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(resident);
        }

        // =========================
        // EDIT GET
        // =========================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents.FindAsync(id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        // =========================
        // EDIT POST
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,FlatNumber,OwnerName,PhoneNumber,Email,IsOwner,CreatedDate")]
            Resident resident)
        {
            if (id != resident.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resident);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResidentExists(resident.Id))
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

            return View(resident);
        }

        // =========================
        // DELETE GET
        // =========================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resident = await _context.Residents
                .FirstOrDefaultAsync(m => m.Id == id);

            if (resident == null)
            {
                return NotFound();
            }

            return View(resident);
        }

        // =========================
        // DELETE POST
        // =========================

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var resident = await _context.Residents.FindAsync(id);

            if (resident != null)
            {
                _context.Residents.Remove(resident);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // EXISTS
        // =========================

        private bool ResidentExists(int? id)
        {
            return _context.Residents.Any(e => e.Id == id);
        }
    }
}