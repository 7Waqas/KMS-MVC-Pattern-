using kms.Models;
using kms.Models.ViewModels;
using kms.Services;
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
        // REPORT 5: Hourly Drill-Down (Chart Click)
        // =============================================
        public async Task<IActionResult> HourlyDrillDown(DateOnly? date, int? hour)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
            var selectedHour = hour ?? DateTime.Now.Hour;

            var hourStart = $"{selectedHour:D2}:00:00";
            var hourEnd = $"{selectedHour:D2}:59:59";

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day &&
                            r.ReportType == 4 &&
                            string.Compare(r.ScanTime, hourStart) >= 0 &&
                            string.Compare(r.ScanTime, hourEnd) <= 0)
                .OrderBy(r => r.ScanTime)
                .ToListAsync();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 4,
                ReportTitle = $"Activity Detail — {selectedDate:dd MMM yyyy} at {selectedHour:D2}:00",
                TotalRecords = data.Count
            };

            ViewBag.SelectedDate = selectedDate;
            ViewBag.SelectedHour = selectedHour;
            ViewBag.HourLabel = $"{selectedHour:D2}:00 – {selectedHour:D2}:59";
            ViewBag.DrillData = data;

            return View(viewModel);
        }

        // =============================================
        // REPORT 1: Morning Keys NOT Taken (date range)
        // =============================================
        public async Task<IActionResult> MorningKeysNotTaken(DateOnly? fromDate, DateOnly? toDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = fromDate ?? today;
            var endDate = toDate ?? startDate;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .OrderBy(k => k.KeyName)
                .ToListAsync();

            // Full morning-scan history (all time), newest first, used to find the
            // "last taken by" person for keys that weren't taken on a given day.
            var morningHistory = await _context.KeyReportData
                .Where(r => r.ReportType == 1)
                .OrderByDescending(r => r.ReportDate)
                .ThenByDescending(r => r.ScanTime)
                .ToListAsync();

            var morningHistoryByKey = morningHistory
                .GroupBy(r => r.KeyName)
                .ToDictionary(g => g.Key, g => g.ToList());

            var notTakenRows = new List<Dictionary<string, object>>();

            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var takenKeyNames = await _context.KeyReportData
                    .Where(r => r.ReportDate == d && r.ReportType == 1)
                    .Select(r => r.KeyName)
                    .ToListAsync();

                foreach (var key in allKeys.Where(k => !takenKeyNames.Contains(k.KeyName)))
                {
                    var lastTakenBy = "--";
                    if (morningHistoryByKey.TryGetValue(key.KeyName, out var history))
                    {
                        var last = history.FirstOrDefault(r => r.ReportDate < d);
                        if (last != null)
                        {
                            lastTakenBy = string.IsNullOrWhiteSpace(last.Employee)
                                ? "--"
                                : $"{last.Employee} (last on {last.ReportDate:dd MMM yyyy})";
                        }
                    }

                    notTakenRows.Add(new Dictionary<string, object>
                    {
                        { "Date", d },
                        { "KeyName", key.KeyName },
                        { "KeyLocation", key.KeyLocation ?? "" },
                        { "TakenBy", lastTakenBy },
                        { "AuthorizedPersons", GetAuthorizedPersons(key.EnrollNumber) }
                    });
                }
            }

            // "Yesterday not returned" alert only applies to the single-day (today) view
            var isSingleDay = startDate == endDate;
            var yesterday = endDate.AddDays(-1);
            var notReturnedYesterdayKeys = new List<KeyMaster>();

            if (isSingleDay)
            {
                var returnedYesterdayKeyNames = await _context.KeyReportData
                    .Where(r => r.ReportDate == yesterday && r.ReportType == 2)
                    .Select(r => r.KeyName)
                    .ToListAsync();

                notReturnedYesterdayKeys = allKeys
                    .Where(k => !returnedYesterdayKeyNames.Contains(k.KeyName))
                    .ToList();
            }

            var viewModel = new ReportViewModel
            {
                SelectedDate = startDate,
                ReportType = 1,
                ReportTitle = "Morning Keys NOT Taken",
                TotalRecords = notTakenRows.Count
            };

            ViewBag.NotTakenRows = notTakenRows;
            ViewBag.FromDate = startDate;
            ViewBag.ToDate = endDate;
            ViewBag.IsSingleDay = isSingleDay;
            ViewBag.NotReturnedYesterdayKeys = notReturnedYesterdayKeys;
            ViewBag.NotReturnedYesterdayCount = notReturnedYesterdayKeys.Count;
            ViewBag.Yesterday = yesterday;

            return View(viewModel);
        }

        // =============================================
        // REPORT 2: Evening Keys NOT Returned (date range)
        // =============================================
        public async Task<IActionResult> EveningKeysNotReturned(DateOnly? fromDate, DateOnly? toDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = fromDate ?? today;
            var endDate = toDate ?? startDate;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var notReturnedRows = new List<Dictionary<string, object>>();

            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                // Raw morning records for the day, kept so we can show who took
                // each key (they're the one still holding it since it wasn't returned).
                var morningRecordsRaw = await _context.KeyReportData
                    .Where(r => r.ReportDate == d && r.ReportType == 1)
                    .ToListAsync();

                var morningByKey = morningRecordsRaw
                    .GroupBy(r => r.KeyName)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ScanTime).First());

                var takenKeyNames = morningByKey.Keys.ToList();

                var returnedKeyNames = await _context.KeyReportData
                    .Where(r => r.ReportDate == d && r.ReportType == 2)
                    .Select(r => r.KeyName)
                    .Distinct()
                    .ToListAsync();

                var notReturnedKeys = await _context.KeyMasters
                    .Where(k => k.IsActive == true &&
                                takenKeyNames.Contains(k.KeyName) &&
                                !returnedKeyNames.Contains(k.KeyName))
                    .ToListAsync();

                foreach (var key in notReturnedKeys)
                {
                    var takenBy = morningByKey.TryGetValue(key.KeyName, out var rec) && !string.IsNullOrWhiteSpace(rec.Employee)
                        ? rec.Employee
                        : "--";

                    notReturnedRows.Add(new Dictionary<string, object>
                    {
                        { "Date", d },
                        { "KeyName", key.KeyName },
                        { "KeyLocation", key.KeyLocation ?? "" },
                        { "ReturnedBy", takenBy },
                        { "AuthorizedPersons", GetAuthorizedPersons(key.EnrollNumber) }
                    });
                }
            }

            var viewModel = new ReportViewModel
            {
                SelectedDate = startDate,
                ReportType = 2,
                ReportTitle = "Evening Keys NOT Returned",
                TotalRecords = notReturnedRows.Count
            };

            ViewBag.NotReturnedRows = notReturnedRows;
            ViewBag.FromDate = startDate;
            ViewBag.ToDate = endDate;
            ViewBag.IsSingleDay = startDate == endDate;

            return View(viewModel);
        }

        // =============================================
        // REPORT 3: Unauthorized Access
        // =============================================
        public async Task<IActionResult> UnauthorizedAccess(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day &&
                            r.ReportType == 3)
                .OrderBy(r => r.ScanTime)
                .ToListAsync();

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

            var allKeys = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .OrderBy(k => k.KeyName)
                .ToListAsync();

            var allReportData = await _context.KeyReportData
                .Where(r => r.ReportDate.Year == selectedDate.Year &&
                            r.ReportDate.Month == selectedDate.Month &&
                            r.ReportDate.Day == selectedDate.Day)
                .ToListAsync();

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