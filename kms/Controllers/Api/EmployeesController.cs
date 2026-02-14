using kms.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Employees
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmployeeMaster>>> GetEmployees()
        {
            return await _context.EmployeeMasters
                .Where(e => e.IsActive == true)
                .ToListAsync();
        }

        // GET: api/Employees/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeMaster>> GetEmployee(int id)
        {
            var employee = await _context.EmployeeMasters.FindAsync(id);

            if (employee == null)
                return NotFound();

            return employee;
        }

        // POST: api/Employees
        [HttpPost]
        public async Task<ActionResult<EmployeeMaster>> CreateEmployee(
            EmployeeMaster employee)
        {
            _context.EmployeeMasters.Add(employee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEmployee),
                new { id = employee.EmpId }, employee);
        }

        // PUT: api/Employees/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(
            int id, EmployeeMaster employee)
        {
            if (id != employee.EmpId)
                return BadRequest();

            _context.Entry(employee).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Employees/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.EmployeeMasters.FindAsync(id);
            if (employee == null)
                return NotFound();

            employee.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EmployeeExists(int id)
        {
            return _context.EmployeeMasters.Any(e => e.EmpId == id);
        }
    }
}