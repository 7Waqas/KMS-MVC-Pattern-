using kms.Models;
using kms.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Main Reports Page
        public IActionResult Index()
        {
            return View();
        }

        // =============================================
        // REPORT 1: Morning Keys NOT Taken
        // =============================================
        public async Task<IActionResult> MorningKeysNotTaken(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
            var yesterday = selectedDate.AddDays(-1);

            // Get all active keys
            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .ToListAsync();

            // Get keys that WERE taken today morning (ReportType = 1)
            var takenTodayKeyNames = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day &&
                            r.ReportType == 1)
                .Select(r => r.KeyName)
                .ToListAsync();

            // Get keys that were NOT returned yesterday evening (ReportType = 2)
            var returnedYesterdayKeyNames = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == yesterday.Year &&
                            r.ReportDate.Month == yesterday.Month &&
                            r.ReportDate.Day == yesterday.Day &&
                            r.ReportType == 2)
                .Select(r => r.KeyName)
                .ToListAsync();

            // Keys not taken today
            var notTakenTodayKeys = allKeys
                .Where(k => !takenTodayKeyNames.Contains(k.KeyName))
                .ToList();

            // Keys not returned yesterday
            var notReturnedYesterdayKeys = allKeys
                .Where(k => !returnedYesterdayKeyNames.Contains(k.KeyName))
                .ToList();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 1,
                ReportTitle = "Morning Keys NOT Taken",
                TotalRecords = notTakenTodayKeys.Count
            };

            ViewBag.NotTakenTodayKeys = notTakenTodayKeys;
            ViewBag.NotReturnedYesterdayKeys = notReturnedYesterdayKeys;
            ViewBag.NotReturnedYesterdayCount = notReturnedYesterdayKeys.Count;
            ViewBag.SelectedDate = selectedDate;
            ViewBag.Yesterday = yesterday;

            return View(viewModel);
        }

        // =============================================
        // REPORT 2: Evening Keys NOT Returned
        // =============================================
        public async Task<IActionResult> EveningKeysNotReturned(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .ToListAsync();

            var returnedKeyNames = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day &&
                            r.ReportType == 2)
                .Select(r => r.KeyName)
                .ToListAsync();

            var notReturnedKeys = allKeys
                .Where(k => !returnedKeyNames.Contains(k.KeyName))
                .ToList();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 2,
                ReportTitle = "Evening Keys NOT Returned",
                TotalRecords = notReturnedKeys.Count
            };

            ViewBag.NotReturnedKeys = notReturnedKeys;
            ViewBag.SelectedDate = selectedDate;

            return View(viewModel);
        }

        // =============================================
        // REPORT 3: Unauthorized Access
        // =============================================
        public async Task<IActionResult> UnauthorizedAccess(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            Console.WriteLine($"UnauthorizedAccess - Selected Date: {selectedDate}");

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day &&
                            r.ReportType == 3)
                .OrderBy(r => r.ScanTime)
                .ToListAsync();

            Console.WriteLine($"UnauthorizedAccess - Found {data.Count} records");

            var enrichedData = new List<Dictionary<string, object>>();

            foreach (var item in data)
            {
                enrichedData.Add(new Dictionary<string, object>
                {
                    { "Employee", item.Employee ?? "Unknown" },
                    { "KeyName", item.KeyName ?? "" },
                    { "ScanTime", item.ScanTime ?? "" },
                    { "ReportDate", item.ReportDate },
                    { "AlertStatus", item.AlertStatus ?? "" },
                    { "AuthorizedPersons", GetAuthorizedPersonsForKey(item.KeyName) }
                });
            }

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 3,
                ReportTitle = "Unauthorized Key Access Alert",
                TotalRecords = data.Count
            };

            ViewBag.UnauthorizedData = enrichedData;
            ViewBag.SelectedDate = selectedDate;

            return View(viewModel);
        }

        private string GetAuthorizedPersonsForKey(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return "--";

            var key = _context.KeyMasters
                .FirstOrDefault(k => k.KeyName == keyName);

            if (key == null) return "--";

            var persons = _context.KeyAuthorizations
                .Where(a => a.KeyEnroll == key.EnrollNumber)
                .Join(_context.EmployeeMasters,
                    ka => ka.EmpEnroll,
                    em => em.EnrollNumber,
                    (ka, em) => em.FullName)
                .ToList();

            return persons.Any() ? string.Join(", ", persons) : "--";
        }

        // =============================================
        // REPORT 4: Full Daily Log
        // =============================================
        public async Task<IActionResult> FullDailyLog(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            Console.WriteLine($"FullDailyLog - Selected Date: {selectedDate}");

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .OrderBy(k => k.KeyName)
                .ToListAsync();

            var allReportData = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day)
                .ToListAsync();

            Console.WriteLine($"FullDailyLog - Total Keys: {allKeys.Count}");
            Console.WriteLine($"FullDailyLog - Report Data Count: {allReportData.Count}");

            var morningData = allReportData.Where(r => r.ReportType == 1).ToList();
            var eveningData = allReportData.Where(r => r.ReportType == 2).ToList();
            var unauthorizedKeys = allReportData.Where(r => r.ReportType == 3)
                .Select(r => r.KeyName).Distinct().ToList();

            var fullLog = new List<Dictionary<string, object>>();

            foreach (var key in allKeys)
            {
                var morningRecord = morningData.FirstOrDefault(m => m.KeyName == key.KeyName);
                var eveningRecord = eveningData.FirstOrDefault(e => e.KeyName == key.KeyName);

                fullLog.Add(new Dictionary<string, object>
                {
                    { "KeyName", key.KeyName },
                    { "KeyLocation", key.KeyLocation ?? "" },
                    { "MorningStatus", morningRecord?.Status ?? "NOT TAKEN" },
                    { "MorningTime", morningRecord?.ScanTime ?? "--" },
                    { "MorningEmployee", morningRecord?.Employee ?? "--" },
                    { "EveningStatus", eveningRecord?.Status ?? "NOT RETURNED" },
                    { "EveningTime", eveningRecord?.ScanTime ?? "--" },
                    { "EveningEmployee", eveningRecord?.Employee ?? "--" },
                    { "AuthorizedPersons", GetAuthorizedPersons(key.EnrollNumber) },
                    { "OverallStatus", unauthorizedKeys.Contains(key.KeyName) ? "UNAUTHORIZED" :
                                      (morningRecord != null || eveningRecord != null) ? "OK" : "NO ACTIVITY" }
                });
            }

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 4,
                ReportTitle = "Full Daily Key Activity Log",
                TotalRecords = fullLog.Count
            };

            ViewBag.FullLog = fullLog;
            ViewBag.SelectedDate = selectedDate;

            Console.WriteLine($"FullDailyLog - Full Log Count: {fullLog.Count}");

            return View(viewModel);
        }

        // =============================================
        // API Endpoints for AJAX/JSON
        // =============================================
        [HttpGet]
        public async Task<IActionResult> GetMorningKeysNotTaken(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .ToListAsync();

            var takenKeyNames = await _context.KeyReportData
                .Where(r => r.ReportDate == selectedDate && r.ReportType == 1)
                .Select(r => r.KeyName)
                .ToListAsync();

            var notTakenKeys = allKeys
                .Where(k => !takenKeyNames.Contains(k.KeyName))
                .Select(k => new
                {
                    keyName = k.KeyName,
                    keyLocation = k.KeyLocation,
                    status = "NOT TAKEN",
                    authorizedPersons = GetAuthorizedPersons(k.EnrollNumber)
                })
                .ToList();

            return Json(notTakenKeys);
        }

        [HttpGet]
        public async Task<IActionResult> GetEveningKeysNotReturned(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .ToListAsync();

            var returnedKeyNames = await _context.KeyReportData
                .Where(r => r.ReportDate == selectedDate && r.ReportType == 2)
                .Select(r => r.KeyName)
                .ToListAsync();

            var notReturnedKeys = allKeys
                .Where(k => !returnedKeyNames.Contains(k.KeyName))
                .Select(k => new
                {
                    keyName = k.KeyName,
                    keyLocation = k.KeyLocation,
                    status = "NOT RETURNED",
                    authorizedPersons = GetAuthorizedPersons(k.EnrollNumber)
                })
                .ToList();

            return Json(notReturnedKeys);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnauthorizedAccess(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate == selectedDate && r.ReportType == 3)
                .OrderBy(r => r.ScanTime)
                .Select(r => new
                {
                    employee = r.Employee,
                    keyName = r.KeyName,
                    scanTime = r.ScanTime,
                    date = r.ReportDate.ToString("dd/MM/yyyy"),
                    alertStatus = r.AlertStatus
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetFullDailyLog(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate == selectedDate && r.ReportType == 4)
                .OrderBy(r => r.ScanTime)
                .Select(r => new
                {
                    employee = r.Employee,
                    keyName = r.KeyName,
                    direction = r.Direction,
                    scanTime = r.ScanTime,
                    authStatus = r.AuthStatus
                })
                .ToListAsync();

            return Json(data);
        }

        private string GetAuthorizedPersons(int keyEnrollNumber)
        {
            var persons = _context.KeyAuthorizations
                .Where(a => a.KeyEnroll == keyEnrollNumber)
                .Join(_context.EmployeeMasters,
                    ka => ka.EmpEnroll,
                    em => em.EnrollNumber,
                    (ka, em) => em.FullName)
                .ToList();

            return persons.Any() ? string.Join(", ", persons) : "--";
        }
    }
}