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
            // Realistic fake data
            var viewModel = new DashboardViewModel
            {
                TotalKeys = 5,
                TotalEmployees = 8,
                KeysNotTakenToday = 2,
                KeysNotReturnedToday = 1,
                UnauthorizedAccessToday = 1,
                RecentActivities = new List<RecentActivity>
                {
                    new RecentActivity
                    {
                        KeyName = "key_IT",
                        Employee = "Waqas",
                        Action = "OUT - Morning",
                        Time = "09:30:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName = "key_Admin",
                        Employee = "Farukh",
                        Action = "IN - Evening",
                        Time = "17:45:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName = "key_R&I",
                        Employee = "Hassan",
                        Action = "OUT - Morning",
                        Time = "08:15:00",
                        IsUnauthorized = false
                    },
                    new RecentActivity
                    {
                        KeyName = "key_IT",
                        Employee = "Unknown",
                        Action = "OUT - Morning",
                        Time = "10:22:00",
                        IsUnauthorized = true
                    }
                }
            };

            Console.WriteLine("📊 Returning MOCK data from memory");
            return Task.FromResult(viewModel);
        }
    }
}