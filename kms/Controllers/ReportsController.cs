using kms.Models;
using kms.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
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

            var sql = @"
                SELECT 
                    km.KeyName,
                    km.KeyLocation,
                    'NOT TAKEN' AS MorningStatus,
                    ISNULL(STUFF((
                        SELECT ', ' + ea.FullName
                        FROM KeyAuthorization ka
                        INNER JOIN EmployeeMaster ea
                            ON ea.EnrollNumber = ka.EmpEnroll
                        WHERE ka.KeyEnroll = km.EnrollNumber
                        FOR XML PATH('')
                    ), 1, 2, ''), '--') AS AuthorizedPersons
                FROM KeyMaster km
                WHERE km.IsActive = 1
                AND NOT EXISTS (
                    SELECT 1 FROM KeyReportData krd
                    WHERE krd.KeyName = km.KeyName
                      AND krd.ReportDate = @SelectedDate
                      AND krd.ReportType = 1
                )
                ORDER BY km.KeyName";

            var data = await _context.Database
                .SqlQueryRaw<dynamic>(sql,
                    new SqlParameter("@SelectedDate", selectedDate))
                .ToListAsync();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 1,
                ReportTitle = "Morning Keys NOT Taken",
                TotalRecords = data.Count
            };

            return View(viewModel);
        }

        // =============================================
        // REPORT 2: Evening Keys NOT Returned
        // =============================================
        public async Task<IActionResult> EveningKeysNotReturned(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            var sql = @"
                SELECT 
                    km.KeyName,
                    km.KeyLocation,
                    'NOT RETURNED' AS EveningStatus,
                    ISNULL(STUFF((
                        SELECT ', ' + ea.FullName
                        FROM KeyAuthorization ka
                        INNER JOIN EmployeeMaster ea
                            ON ea.EnrollNumber = ka.EmpEnroll
                        WHERE ka.KeyEnroll = km.EnrollNumber
                        FOR XML PATH('')
                    ), 1, 2, ''), '--') AS AuthorizedPersons
                FROM KeyMaster km
                WHERE km.IsActive = 1
                AND NOT EXISTS (
                    SELECT 1 FROM KeyReportData krd
                    WHERE krd.KeyName = km.KeyName
                      AND krd.ReportDate = @SelectedDate
                      AND krd.ReportType = 2
                )
                ORDER BY km.KeyName";

            var data = await _context.Database
                .SqlQueryRaw<dynamic>(sql,
                    new SqlParameter("@SelectedDate", selectedDate))
                .ToListAsync();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 2,
                ReportTitle = "Evening Keys NOT Returned",
                TotalRecords = data.Count
            };

            return View(viewModel);
        }

        // =============================================
        // REPORT 3: Unauthorized Access
        // =============================================
        public async Task<IActionResult> UnauthorizedAccess(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            var data = await _context.KeyReportData
                .Where(r => r.ReportDate == selectedDate && r.ReportType == 3)
                .OrderBy(r => r.ScanTime)
                .ToListAsync();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 3,
                ReportTitle = "Unauthorized Key Access Alert",
                TotalRecords = data.Count
            };

            return View(viewModel);
        }

        // =============================================
        // REPORT 4: Full Daily Log
        // =============================================
        public async Task<IActionResult> FullDailyLog(DateOnly? date)
        {
            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            var sql = @"
                SELECT 
                    km.KeyName,
                    km.KeyLocation,
                    ISNULL(
                        (SELECT TOP 1 'OUT - Taken (' + 
                             ISNULL(Employee, 'Unknown') + ')'
                         FROM KeyReportData
                         WHERE KeyName = km.KeyName
                           AND ReportDate = @SelectedDate
                           AND ReportType = 1),
                        'NOT TAKEN'
                    ) AS MorningStatus,
                    ISNULL(
                        (SELECT TOP 1 ScanTime
                         FROM KeyReportData
                         WHERE KeyName = km.KeyName
                           AND ReportDate = @SelectedDate
                           AND ReportType = 1),
                        '--'
                    ) AS MorningTime,
                    ISNULL(
                        (SELECT TOP 1 'IN - Returned (' + 
                             ISNULL(Employee, 'Unknown') + ')'
                         FROM KeyReportData
                         WHERE KeyName = km.KeyName
                           AND ReportDate = @SelectedDate
                           AND ReportType = 2),
                        'NOT RETURNED'
                    ) AS EveningStatus,
                    ISNULL(
                        (SELECT TOP 1 ScanTime
                         FROM KeyReportData
                         WHERE KeyName = km.KeyName
                           AND ReportDate = @SelectedDate
                           AND ReportType = 2),
                        '--'
                    ) AS EveningTime,
                    ISNULL(STUFF((
                        SELECT ', ' + ea.FullName
                        FROM KeyAuthorization ka
                        INNER JOIN EmployeeMaster ea
                            ON ea.EnrollNumber = ka.EmpEnroll
                        WHERE ka.KeyEnroll = km.EnrollNumber
                        FOR XML PATH('')
                    ), 1, 2, ''), '--') AS AuthorizedPersons,
                    CASE 
                        WHEN EXISTS (
                            SELECT 1 FROM KeyReportData
                            WHERE KeyName = km.KeyName
                              AND ReportDate = @SelectedDate
                              AND ReportType = 3
                        ) THEN 'UNAUTHORIZED'
                        WHEN EXISTS (
                            SELECT 1 FROM KeyReportData
                            WHERE KeyName = km.KeyName
                              AND ReportDate = @SelectedDate
                              AND ReportType IN (1, 2)
                        ) THEN 'OK'
                        ELSE 'NO ACTIVITY'
                    END AS OverallStatus
                FROM KeyMaster km
                WHERE km.IsActive = 1
                ORDER BY km.KeyName";

            var data = await _context.Database
                .SqlQueryRaw<dynamic>(sql,
                    new SqlParameter("@SelectedDate", selectedDate))
                .ToListAsync();

            var viewModel = new ReportViewModel
            {
                SelectedDate = selectedDate,
                ReportType = 4,
                ReportTitle = "Full Daily Key Activity Log",
                TotalRecords = data.Count
            };

            return View(viewModel);
        }

        // =============================================
        // API Endpoints for AJAX/JSON
        // =============================================
        [HttpGet]
        public async Task<IActionResult> GetMorningKeysNotTaken(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var data = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .Where(k => !_context.KeyReportData
                    .Any(r => r.KeyName == k.KeyName &&
                              r.ReportDate == selectedDate &&
                              r.ReportType == 1))
                .Select(k => new
                {
                    k.KeyName,
                    k.KeyLocation,
                    Status = "NOT TAKEN",
                    AuthorizedPersons = string.Join(", ",
                        _context.KeyAuthorizations
                            .Where(a => a.KeyEnroll == k.EnrollNumber)
                            .Join(_context.EmployeeMasters,
                                ka => ka.EmpEnroll,
                                em => em.EnrollNumber,
                                (ka, em) => em.FullName))
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetEveningKeysNotReturned(string date)
        {
            var selectedDate = DateOnly.Parse(date);

            var data = await _context.KeyMasters
                .Where(k => k.IsActive == true)
                .Where(k => !_context.KeyReportData
                    .Any(r => r.KeyName == k.KeyName &&
                              r.ReportDate == selectedDate &&
                              r.ReportType == 2))
                .Select(k => new
                {
                    k.KeyName,
                    k.KeyLocation,
                    Status = "NOT RETURNED",
                    AuthorizedPersons = string.Join(", ",
                        _context.KeyAuthorizations
                            .Where(a => a.KeyEnroll == k.EnrollNumber)
                            .Join(_context.EmployeeMasters,
                                ka => ka.EmpEnroll,
                                em => em.EnrollNumber,
                                (ka, em) => em.FullName))
                })
                .ToListAsync();

            return Json(data);
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
                    r.Employee,
                    r.KeyName,
                    r.ScanTime,
                    Date = r.ReportDate.ToString("dd/MM/yyyy"),
                    r.AlertStatus
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
                    r.Employee,
                    r.KeyName,
                    r.Direction,
                    r.ScanTime,
                    r.AuthStatus
                })
                .ToListAsync();

            return Json(data);
        }
    }
}