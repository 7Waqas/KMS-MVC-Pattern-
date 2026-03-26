using kms.Models;
using kms.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════
// ADD AUTHENTICATION SERVICE
// ═══════════════════════════════════════════════════
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "KMS.Auth";
    });


// Add services to the container
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling =
            Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

// ═══════════════════════════════════════════════════
// DATABASE CONFIGURATION - SWITCH BETWEEN HOME/OFFICE
// ═══════════════════════════════════════════════════

var useMockData = builder.Configuration.GetValue<bool>("UseMockData");

if (useMockData)
{
    // ╔════════════════════════════════════════╗
    // ║  HOME MODE - MOCK DATA (NO DATABASE)   ║
    // ╚════════════════════════════════════════╝

    // Register Mock Dashboard Service
    builder.Services.AddScoped<IDashboardService, MockDashboardService>();

    // Register a FAKE DbContext (doesn't connect to database)
    // This prevents errors when controllers need it
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("MockDatabase"));

    Console.WriteLine("=========================================");
    Console.WriteLine("    HOME MODE - MOCK DATA ACTIVE");
    Console.WriteLine("    Database: IN-MEMORY (FAKE)");
    Console.WriteLine("=========================================");
    Console.WriteLine();
}
else
{
    // ╔════════════════════════════════════════╗
    // ║  OFFICE MODE - REAL DATABASE           ║
    // ╚════════════════════════════════════════╝

    // Register Real Database Context
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));

    // Register Real Database Service
    builder.Services.AddScoped<IDashboardService, DatabaseDashboardService>();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var server = connectionString?.Split(';')[0].Replace("Server=", "");

    Console.WriteLine("=========================================");
    Console.WriteLine("    OFFICE MODE - REAL DATABASE");
    Console.WriteLine($"    Server: {server}");
    Console.WriteLine("=========================================");
    Console.WriteLine();
}



//// Add services
//builder.Services.AddControllersWithViews()
//    .AddNewtonsoftJson(options =>
//    {
//        options.SerializerSettings.ReferenceLoopHandling =
//            Newtonsoft.Json.ReferenceLoopHandling.Ignore;
//    });

//// Database Context
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddResponseCompression();

// OpenAPI/Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Seed mock data if in mock mode
if (builder.Configuration.GetValue<bool>("UseMockData"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Add mock employees
        if (!context.EmployeeMasters.Any())
        {
            context.EmployeeMasters.AddRange(
                new EmployeeMaster
                {
                    EnrollNumber = 1,
                    FullName = "Waqas",
                    Department = "IT",
                    IsActive = true
                },
                new EmployeeMaster
                {
                    EnrollNumber = 2,
                    FullName = "Farukh",
                    Department = "R&I",
                    IsActive = true
                },
                new EmployeeMaster
                {
                    EnrollNumber = 3,
                    FullName = "Hassan",
                    Department = "IT",
                    IsActive = true
                }
            );
        }

        // Add mock keys
        if (!context.KeyMasters.Any())
        {
            context.KeyMasters.AddRange(
                new KeyMaster
                {
                    EnrollNumber = 1001,
                    KeyName = "key_IT",
                    KeyLocation = "IT Department",
                    IsActive = true
                },
                new KeyMaster
                {
                    EnrollNumber = 1002,
                    KeyName = "key_R&I",
                    KeyLocation = "R&I Department",
                    IsActive = true
                },
                new KeyMaster
                {
                    EnrollNumber = 1003,
                    KeyName = "key_Admin",
                    KeyLocation = "Admin Office",
                    IsActive = true
                }
            );
        }

        // Add mock authorizations
        if (!context.KeyAuthorizations.Any())
        {
            context.KeyAuthorizations.AddRange(
                new KeyAuthorization
                {
                    KeyEnroll = 1001,
                    EmpEnroll = 1,
                    AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                },
                new KeyAuthorization
                {
                    KeyEnroll = 1001,
                    EmpEnroll = 3,
                    AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                },
                new KeyAuthorization
                {
                    KeyEnroll = 1002,
                    EmpEnroll = 2,
                    AssignedDate = DateOnly.FromDateTime(DateTime.Today)
                }
            );
        }

        // Add mock report data
        if (!context.KeyReportData.Any())
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            context.KeyReportData.AddRange(

                // ── ReportType 1 (Morning taken) ──
                new KeyReportData { ReportType = 1, ReportDate = today, KeyName = "key_IT", ScanTime = "08:15:00", Employee = "Waqas", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 1, ReportDate = today, KeyName = "key_R&I", ScanTime = "09:02:00", Employee = "Farukh", AuthStatus = "Authorized" },

                // ── ReportType 2 (Evening returned) ──
                new KeyReportData { ReportType = 2, ReportDate = today, KeyName = "key_IT", Status = "IN - Returned", ScanTime = "16:46:00", Employee = "Waqas", AuthorizedPersons = "Waqas, Hassan", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 2, ReportDate = today, KeyName = "key_R&I", Status = "IN - Returned", ScanTime = "16:46:07", Employee = "Waqas", AuthorizedPersons = "Farukh", AuthStatus = "UNAUTHORIZED" },

                // ── ReportType 3 (Unauthorized log) ──
                new KeyReportData { ReportType = 3, ReportDate = today, KeyName = "key_R&I", ScanTime = "11:30:00", Employee = "Waqas", AuthStatus = "UNAUTHORIZED" },
                new KeyReportData { ReportType = 3, ReportDate = today, KeyName = "key_Admin", ScanTime = "14:10:00", Employee = "Hassan", AuthStatus = "UNAUTHORIZED" },

                // ── ReportType 4 (Activity log for chart & recent table) ──

                // 08:00 morning rush — keys going OUT
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "OUT - Morning", ScanTime = "08:05:00", Employee = "Waqas", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "OUT - Morning", ScanTime = "08:15:00", Employee = "Hassan", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_R&I", Direction = "OUT - Morning", ScanTime = "08:45:00", Employee = "Farukh", AuthStatus = "Authorized" },

                // 09:00 — more going OUT
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "OUT - Morning", ScanTime = "09:10:00", Employee = "Hassan", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "OUT - Morning", ScanTime = "09:30:00", Employee = "Waqas", AuthStatus = "Authorized" },

                // 11:00 — unauthorized spike
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_R&I", Direction = "OUT - Morning", ScanTime = "11:28:00", Employee = "Waqas", AuthStatus = "UNAUTHORIZED" },

                // 12:00 — lunch returns
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "IN - Return", ScanTime = "12:05:00", Employee = "Waqas", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "IN - Return", ScanTime = "12:20:00", Employee = "Waqas", AuthStatus = "Authorized" },

                // 13:00 — out again after lunch
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "OUT - Morning", ScanTime = "13:05:00", Employee = "Waqas", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "OUT - Morning", ScanTime = "13:40:00", Employee = "Hassan", AuthStatus = "Authorized" },

                // 14:00 — another unauthorized
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "OUT - Morning", ScanTime = "14:08:00", Employee = "Hassan", AuthStatus = "UNAUTHORIZED" },

                // 16:00 — evening return spike
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "IN - Evening", ScanTime = "16:30:00", Employee = "Waqas", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_R&I", Direction = "IN - Evening", ScanTime = "16:46:03", Employee = "Farukh", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "IN - Evening", ScanTime = "16:46:07", Employee = "Hassan", AuthStatus = "Authorized" },

                // 17:00 — late returns
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_IT", Direction = "IN - Evening", ScanTime = "17:10:00", Employee = "Hassan", AuthStatus = "Authorized" },
                new KeyReportData { ReportType = 4, ReportDate = today, KeyName = "key_Admin", Direction = "IN - Evening", ScanTime = "17:45:00", Employee = "Waqas", AuthStatus = "Authorized" }
            );
        }

        context.SaveChanges();
        Console.WriteLine("✅ Mock data seeded successfully!");
    }
}
// Configure middleware 
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ═══════════════════════════════════════════════════
// ADD AUTHENTICATION & AUTHORIZATION MIDDLEWARE
// Must be after UseRouting and before MapControllerRoute
// ═══════════════════════════════════════════════════
app.UseAuthentication();
app.UseAuthorization();


// ═══════════════════════════════════════════════════
// ROOT URL REDIRECT TO LOGIN
// ═══════════════════════════════════════════════════
app.MapGet("/", context =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Home/Index");
    }
    else
    {
        context.Response.Redirect("/Account/Login");
    }
    return Task.CompletedTask;
});

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Enable OpenAPI and Scalar (available in both dev and production)
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Key Management System API")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.Run();
