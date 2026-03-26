namespace kms.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalKeys { get; set; }
        public int TotalEmployees { get; set; }
        public int KeysNotTakenToday { get; set; }
        public int KeysNotReturnedToday { get; set; }
        public int UnauthorizedAccessToday { get; set; }
        public List<RecentActivity> RecentActivities { get; set; } = new();

        // Hourly activity chart data (06:00 to 20:00 = 15 hours)
        public List<int> HourlyKeyOut { get; set; } = new();
        public List<int> HourlyKeyReturned { get; set; } = new();
        public List<int> HourlyUnauthorized { get; set; } = new();
    }

    public class RecentActivity
    {
        public string KeyName { get; set; } = string.Empty;
        public string Employee { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public bool IsUnauthorized { get; set; }
    }
}