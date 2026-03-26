using kms.Models;
using kms.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace kms.Services
{
    public class DatabaseDashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DatabaseDashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Pull all today's ReportType==4 (activity) records once
            var todayActivities = await _context.KeyReportData
                .Where(r => r.ReportDate == today && r.ReportType == 4)
                .Select(r => new
                {
                    r.Direction,
                    r.ScanTime,
                    r.AuthStatus,
                    r.KeyName
                })
                .ToListAsync();

            // Hour slots 0 to 24
            var hourSlots = Enumerable.Range(0, 24).ToList();

            var hourlyKeyOut = hourSlots.Select(h =>
    todayActivities.Where(r =>
        r.Direction != null &&
        r.Direction.Contains("OUT") &&
        TryParseHour(r.ScanTime) == h)
    .GroupBy(r => r.KeyName).Distinct().Count())
    .ToList();

            var hourlyKeyReturned = hourSlots.Select(h =>
                todayActivities.Where(r =>
                    r.Direction != null &&
                    r.Direction.Contains("IN") &&
                    TryParseHour(r.ScanTime) == h)
                .GroupBy(r => r.KeyName).Distinct().Count())
                .ToList();

            var hourlyUnauthorized = hourSlots.Select(h =>
                todayActivities.Where(r =>
                    r.AuthStatus != null &&
                    r.AuthStatus.Contains("UNAUTHORIZED") &&
                    TryParseHour(r.ScanTime) == h)
                .GroupBy(r => r.KeyName).Distinct().Count())
                .ToList();

            var viewModel = new DashboardViewModel
            {
                TotalKeys = await _context.KeyMasters
                    .Where(k => k.IsActive == true)
                    .CountAsync(),

                TotalEmployees = await _context.EmployeeMasters
                    .Where(e => e.IsActive == true)
                    .CountAsync(),

                KeysNotTakenToday = await _context.KeyMasters
                    .Where(k => k.IsActive == true)
                    .Where(k => !_context.KeyReportData
                        .Any(r => r.KeyName == k.KeyName &&
                                  r.ReportDate == today &&
                                  r.ReportType == 1))
                    .CountAsync(),

                KeysNotReturnedToday = await _context.KeyMasters
                    .Where(k => k.IsActive == true)
                    .Where(k => !_context.KeyReportData
                        .Any(r => r.KeyName == k.KeyName &&
                                  r.ReportDate == today &&
                                  r.ReportType == 2))
                    .CountAsync(),

                UnauthorizedAccessToday = await _context.KeyReportData
                    .Where(r => r.ReportDate == today && r.ReportType == 3)
                    .CountAsync(),

                RecentActivities = await _context.KeyReportData
                    .Where(r => r.ReportDate == today && r.ReportType == 4)
                    .OrderByDescending(r => r.ScanTime)
                    .Take(10)
                    .Select(r => new RecentActivity
                    {
                        KeyName = r.KeyName ?? "",
                        Employee = r.Employee ?? "",
                        Action = r.Direction ?? "",
                        Time = r.ScanTime ?? "",
                        IsUnauthorized = r.AuthStatus != null &&
                                         r.AuthStatus.Contains("UNAUTHORIZED")
                    })
                    .ToListAsync(),

                HourlyKeyOut = hourlyKeyOut,
                HourlyKeyReturned = hourlyKeyReturned,
                HourlyUnauthorized = hourlyUnauthorized
            };

            return viewModel;
        }

        // Safely parse hour from time strings like "08:45", "8:45 AM", "08:45:00"
        private static int TryParseHour(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return -1;
            if (DateTime.TryParse(timeStr, out var dt)) return dt.Hour;
            if (TimeSpan.TryParse(timeStr, out var ts)) return ts.Hours;
            return -1;
        }
    }
}