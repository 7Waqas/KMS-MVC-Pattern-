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
                    .Where(r => r.ReportDate == today &&
                                r.ReportType == 3)
                    .CountAsync(),

                RecentActivities = await _context.KeyReportData
                    .Where(r => r.ReportDate == today &&
                                r.ReportType == 4)
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
                    .ToListAsync()
            };

            return viewModel;
        }
    }
}