using kms.Models;
using kms.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
                new KeyReportData
                {
                    ReportType = 2,
                    ReportDate = today,
                    KeyName = "key_IT",
                    Status = "IN - Returned",
                    ScanTime = "16:46:03",
                    Employee = "Waqas",
                    AuthorizedPersons = "Waqas, Hassan",
                    AuthStatus = "Authorized"
                },
                new KeyReportData
                {
                    ReportType = 2,
                    ReportDate = today,
                    KeyName = "key_R&I",
                    Status = "IN - Returned",
                    ScanTime = "16:46:07",
                    Employee = "Waqas",
                    AuthorizedPersons = "Farukh",
                    AuthStatus = "UNAUTHORIZED"
                },
                new KeyReportData
                {
                    ReportType = 4,
                    ReportDate = today,
                    KeyName = "key_IT",
                    Direction = "IN - Evening",
                    ScanTime = "16:46:03",
                    Employee = "Waqas",
                    AuthStatus = "Authorized"
                },
                new KeyReportData
                {
                    ReportType = 4,
                    ReportDate = today,
                    KeyName = "key_R&I",
                    Direction = "IN - Evening",
                    ScanTime = "16:46:07",
                    Employee = "Waqas",
                    AuthStatus = "UNAUTHORIZED"
                }
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

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