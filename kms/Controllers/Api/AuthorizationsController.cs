using kms.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthorizationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Authorizations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAuthorizations()
        {
            var auths = await _context.KeyAuthorizations
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
                        x.ka.EmpEnroll,
                        EmployeeName = em.FullName,
                        x.ka.AssignedDate
                    })
                .ToListAsync();

            return Ok(auths);
        }

        // POST: api/Authorizations
        [HttpPost]
        public async Task<ActionResult<KeyAuthorization>> CreateAuthorization(
            KeyAuthorization authorization)
        {
            // Check if already exists
            var exists = await _context.KeyAuthorizations
                .AnyAsync(a => a.KeyEnroll == authorization.KeyEnroll &&
                               a.EmpEnroll == authorization.EmpEnroll);

            if (exists)
                return Conflict("Authorization already exists");

            _context.KeyAuthorizations.Add(authorization);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAuthorizations),
                new { id = authorization.AuthId }, authorization);
        }

        // DELETE: api/Authorizations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAuthorization(int id)
        {
            var auth = await _context.KeyAuthorizations.FindAsync(id);
            if (auth == null)
                return NotFound();

            _context.KeyAuthorizations.Remove(auth);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Authorizations/Assign
        [HttpPost("Assign")]
        public async Task<IActionResult> AssignKeyToEmployee(
            [FromBody] AssignKeyRequest request)
        {
            // Remove existing authorizations
            var existing = _context.KeyAuthorizations
                .Where(a => a.KeyEnroll == request.KeyEnrollNumber);
            _context.KeyAuthorizations.RemoveRange(existing);

            // Add new authorizations
            foreach (var empId in request.EmployeeEnrollNumbers)
            {
                _context.KeyAuthorizations.Add(new KeyAuthorization
                {
                    KeyEnroll = request.KeyEnrollNumber,
                    EmpEnroll = empId,
                    AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    public class AssignKeyRequest
    {
        public int KeyEnrollNumber { get; set; }
        public List<int> EmployeeEnrollNumbers { get; set; } = new();
    }
}