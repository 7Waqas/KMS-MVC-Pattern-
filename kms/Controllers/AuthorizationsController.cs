using kms.Models;
using kms.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
    [Authorize]
    public class AuthorizationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthorizationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Authorizations
        public async Task<IActionResult> Index()
        {
            var authorizations = await _context.KeyAuthorizations
                .Join(_context.KeyMasters,
                    ka => ka.KeyEnroll,
                    km => km.EnrollNumber,
                    (ka, km) => new { ka, km })
                .Join(_context.EmployeeMasters,
                    x => x.ka.EmpEnroll,
                    em => em.EnrollNumber,
                    (x, em) => new
                    {
                        x.ka.AuthId,
                        x.ka.KeyEnroll,
                        KeyName = x.km.KeyName,
                        KeyLocation = x.km.KeyLocation,
                        x.ka.EmpEnroll,
                        EmployeeName = em.FullName,
                        Department = em.Department,
                        x.ka.AssignedDate
                    })
                .OrderBy(a => a.KeyName)
                .ThenBy(a => a.EmployeeName)
                .ToListAsync();

            return View(authorizations);
        }

        // GET: Authorizations/Manage/1001
        public async Task<IActionResult> Manage(int? keyEnroll)
        {
            if (keyEnroll == null)
            {
                // Show all keys to select from
                var keys = await _context.KeyMasters
                    .Where(k => k.IsActive == true)
                    .OrderBy(k => k.KeyName)
                    .ToListAsync();

                ViewBag.Keys = new SelectList(keys, "EnrollNumber", "KeyName");
                return View("SelectKey");
            }

            var key = await _context.KeyMasters
                .FirstOrDefaultAsync(k => k.EnrollNumber == keyEnroll);

            if (key == null)
                return NotFound();

            var currentAuthorized = await _context.KeyAuthorizations
                .Where(ka => ka.KeyEnroll == keyEnroll)
                .Join(_context.EmployeeMasters,
                    ka => ka.EmpEnroll,
                    em => em.EnrollNumber,
                    (ka, em) => em)
                .ToListAsync();

            var allEmployees = await _context.EmployeeMasters
                .Where(e => e.IsActive == true)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var viewModel = new KeyAuthViewModel
            {
                KeyId = key.KeyId,
                KeyName = key.KeyName,
                CurrentAuthorizedEmployees = currentAuthorized,
                AvailableEmployees = allEmployees,
                AuthorizedEmployeeIds = currentAuthorized
                    .Select(e => e.EnrollNumber)
                    .ToList()
            };

            return View(viewModel);
        }

        // POST: Authorizations/UpdateAuthorizations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAuthorizations(
            int keyEnroll, List<int> employeeEnrollNumbers)
        {
            // Remove all existing authorizations for this key
            var existing = _context.KeyAuthorizations
                .Where(ka => ka.KeyEnroll == keyEnroll);
            _context.KeyAuthorizations.RemoveRange(existing);

            // Add new authorizations
            if (employeeEnrollNumbers != null && employeeEnrollNumbers.Any())
            {
                foreach (var empEnroll in employeeEnrollNumbers)
                {
                    _context.KeyAuthorizations.Add(new KeyAuthorization
                    {
                        KeyEnroll = keyEnroll,
                        EmpEnroll = empEnroll,
                        AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Authorizations updated successfully!";

            return RedirectToAction(nameof(Index));
        }
        // GET: /Authorizations/ManageByEmployee?empEnroll=50
        public IActionResult ManageByEmployee(int empEnroll)
        {
            var employee = _context.EmployeeMasters
                                   .FirstOrDefault(e => e.EnrollNumber == empEnroll);
            if (employee == null) return NotFound();

            var allKeys = _context.KeyMasters
                                  .Where(k => k.IsActive == true)
                                  .OrderBy(k => k.KeyName)
                                  .ToList();

            var authorizedKeyEnrolls = _context.KeyAuthorizations
                                               .Where(a => a.EmpEnroll == empEnroll)
                                               .Select(a => a.KeyEnroll)
                                               .ToList();

            var vm = new EmployeeAuthViewModel
            {
                EmployeeName = employee.FullName,
                EmpEnroll = empEnroll,
                AvailableKeys = allKeys,
                AuthorizedKeyEnrolls = authorizedKeyEnrolls
            };

            return View(vm);
        }

        // POST: /Authorizations/UpdateAuthorizationsByEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAuthorizationsByEmployee(int empEnroll,
                                                            List<int> keyEnrollNumbers)
        {
            var existing = _context.KeyAuthorizations
                                   .Where(a => a.EmpEnroll == empEnroll)
                                   .ToList();
            _context.KeyAuthorizations.RemoveRange(existing);

            if (keyEnrollNumbers != null && keyEnrollNumbers.Any())
            {
                foreach (var keyEnroll in keyEnrollNumbers)
                {
                    _context.KeyAuthorizations.Add(new KeyAuthorization
                    {
                        EmpEnroll = empEnroll,
                        KeyEnroll = keyEnroll,
                        AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                    });
                }
            }

            _context.SaveChanges();
            TempData["Success"] = "Key authorizations updated successfully.";
            return RedirectToAction("Index", "Employees");
        }

        // POST: Authorizations/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var auth = await _context.KeyAuthorizations.FindAsync(id);
            if (auth != null)
            {
                _context.KeyAuthorizations.Remove(auth);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Authorization removed successfully!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}