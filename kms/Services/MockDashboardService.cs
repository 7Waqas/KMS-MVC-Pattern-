using kms.Models.ViewModels;

namespace kms.Services
{
    public class MockDashboardService : IDashboardService
    {
        public MockDashboardService()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🎭 MOCK DATA SERVICE ACTIVE");
            Console.WriteLine("🏠 Working from HOME mode");
            Console.WriteLine("⚠️  NO DATABASE CONNECTION");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        public Task<DashboardViewModel> GetDashboardDataAsync()
        {
            // Hour slots 06:00 → 20:00  (index 0 = 00:00, index 1 = 07:00 … index 14 = 20:00)
            //                            0  07  08  09  10  11  12  13  14  15  16  17  18  19  20.....24
            var hourlyKeyOut = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 1, 1, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            var hourlyKeyReturned = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 1, 3, 2, 0, 0, 0, 0, 0, 0 };
            var hourlyUnauthorized = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            var viewModel = new DashboardViewModel
            {
                TotalKeys = 5,
                TotalEmployees = 8,
                KeysNotTakenToday = 2,
                KeysNotReturnedToday = 1,
                UnauthorizedAccessToday = 1,

                HourlyKeyOut = hourlyKeyOut,
                HourlyKeyReturned = hourlyKeyReturned,
                HourlyUnauthorized = hourlyUnauthorized,

                RecentActivities = new List<RecentActivity>
                {
                    new RecentActivity
                    {
                        KeyName      = "key_IT",
                        Employee     = "Waqas",
                        Action       = "OUT - Morning",
                        Time         = "09:30:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName      = "key_Admin",
                        Employee     = "Farukh",
                        Action       = "IN - Evening",
                        Time         = "17:45:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName      = "key_R&I",
                        Employee     = "Hassan",
                        Action       = "OUT - Morning",
                        Time         = "08:15:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName      = "key_IT",
                        Employee     = "Unknown",
                        Action       = "OUT - Morning",
                        Time         = "10:22:00",
                        IsUnauthorized = true
                    }
                }
            };

            Console.WriteLine("📊 Returning MOCK data from memory");
            return Task.FromResult(viewModel);
        }
    }
}