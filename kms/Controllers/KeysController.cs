using kms.Models;
using kms.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
    [Authorize]
    public class KeysController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KeysController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Keys
        public async Task<IActionResult> Index()
        {
            var keys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .OrderBy(k => k.KeyName)
                .ToListAsync();

            return View(keys);
        }

        // GET: Keys/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Keys/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KeyMaster key)
        {
            if (ModelState.IsValid)
            {
                key.IsActive = true;
                _context.Add(key);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Key created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(key);
        }

        // GET: Keys/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var key = await _context.KeyMasters.FindAsync(id);
            if (key == null)
                return NotFound();

            return View(key);
        }

        // POST: Keys/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KeyMaster key)
        {
            if (id != key.KeyId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(key);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Key updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!KeyExists(key.KeyId))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(key);
        }

        // POST: Keys/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var key = await _context.KeyMasters.FindAsync(id);
            if (key != null)
            {
                key.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Key deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool KeyExists(int id)
        {
            return _context.KeyMasters.Any(k => k.KeyId == id);
        }
    }
}