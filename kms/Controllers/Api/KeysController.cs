using kms.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class KeysController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public KeysController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Keys
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetKeys()
        {
            var keys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .Select(k => new
                {
                    k.KeyId,
                    k.EnrollNumber,
                    k.KeyName,
                    k.KeyLocation,
                    k.IsActive,
                    AuthorizedEmployees = _context.KeyAuthorizations
                        .Where(a => a.KeyEnroll == k.EnrollNumber)
                        .Join(_context.EmployeeMasters,
                            ka => ka.EmpEnroll,
                            em => em.EnrollNumber,
                            (ka, em) => em.FullName)
                        .ToList()
                })
                .ToListAsync();

            return Ok(keys);
        }

        // GET: api/Keys/5
        [HttpGet("{id}")]
        public async Task<ActionResult<KeyMaster>> GetKey(int id)
        {
            var key = await _context.KeyMasters.FindAsync(id);

            if (key == null)
                return NotFound();

            return key;
        }

        // POST: api/Keys
        [HttpPost]
        public async Task<ActionResult<KeyMaster>> CreateKey(KeyMaster key)
        {
            _context.KeyMasters.Add(key);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetKey),
                new { id = key.KeyId }, key);
        }

        // PUT: api/Keys/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateKey(int id, KeyMaster key)
        {
            if (id != key.KeyId)
                return BadRequest();

            _context.Entry(key).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!KeyExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Keys/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteKey(int id)
        {
            var key = await _context.KeyMasters.FindAsync(id);
            if (key == null)
                return NotFound();

            key.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool KeyExists(int id)
        {
            return _context.KeyMasters.Any(k => k.KeyId == id);
        }
    }
}