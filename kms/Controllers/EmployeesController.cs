using kms.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
    [Authorize]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }
        // Helper: Load departments into ViewBag
        // Called by Create and Edit GET methods
        // =============================================
        private void LoadDepartments()
        {
            // All distinct departments already in database
            var departments = _context.EmployeeMasters
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Count active employees per department (for sidebar)
            var deptCounts = _context.EmployeeMasters
                .Where(e => e.IsActive == true && e.Department != null)
                .GroupBy(e => e.Department)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Department, x => x.Count);

            ViewBag.Departments = departments;
            ViewBag.DeptCounts = deptCounts;
        }
        // GET: Employees
        public async Task<IActionResult> Index()
        {
            var employees = await _context.EmployeeMasters
                .Where(e => e.IsActive == true)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            return View(employees);
        }

        // GET: Employees/Create
        public IActionResult Create()
        {
            LoadDepartments();
            return View();
        }

        // POST: Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeMaster employee)
        {
            if (ModelState.IsValid)
            {
                employee.IsActive = true;
                _context.Add(employee);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Employee created successfully!";
                return RedirectToAction(nameof(Index));
            }
            LoadDepartments();
            return View(employee);
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.EmployeeMasters.FindAsync(id);
            if (employee == null)
                return NotFound();

            LoadDepartments();
            return View(employee);
        }

        // POST: Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeMaster employee)
        {
            if (id != employee.EmpId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Employee updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.EmpId))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            LoadDepartments();
            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.EmployeeMasters.FindAsync(id);
            if (employee != null)
            {
                employee.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Employee deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.EmployeeMasters.Any(e => e.EmpId == id);
        }
    }
}