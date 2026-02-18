using kms.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace kms.Controllers
{
    public class AccountController : Controller
    {
        // =============================================
        // HARDCODED ADMIN CREDENTIALS
        // TODO: Change these to your secure values
        // =============================================
        private const string ADMIN_USERNAME = "admin";
        private const string ADMIN_PASSWORD = "Admin@123";

        // =============================================
        // GET: /Account/Login
        // =============================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // =============================================
        // POST: /Account/Login
        // =============================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate credentials
            if (model.Username == ADMIN_USERNAME && model.Password == ADMIN_PASSWORD)
            {
                // Create authentication claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("FullName", "System Administrator")
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : DateTimeOffset.UtcNow.AddHours(8)
                };

                // Sign in the user
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Success message
                TempData["Success"] = $"Welcome back, {model.Username}!";

                // Redirect
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            // Invalid credentials
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        // =============================================
        // POST: /Account/Logout
        // =============================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // =============================================
        // GET: /Account/AccessDenied
        // =============================================
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}