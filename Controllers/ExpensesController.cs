using ApartmentManagementSystem.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagementSystem.Data;
using ApartmentManagementSystem.Models;

namespace ApartmentManagementSystem.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,Resident")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Expenses.ToListAsync());
        }

        [Authorize(Roles = "Admin,Resident")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(m => m.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Create(
            [Bind("Id,ExpenseType,Amount,ExpenseDate,Description")]
            Expense expense)
        {
            if (ModelState.IsValid)
            {
                _context.Add(expense);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(expense);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses.FindAsync(id);

            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,ExpenseType,Amount,ExpenseDate,Description")]
            Expense expense)
        {
            if (id != expense.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(expense);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExpenseExists(expense.Id))
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

            return View(expense);
        }

        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(m => m.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [RequireCommitteeAdmin]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var expense = await _context.Expenses.FindAsync(id);

            if (expense != null)
            {
                _context.Expenses.Remove(expense);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool ExpenseExists(int id)
        {
            return _context.Expenses.Any(e => e.Id == id);
        }
    }
}