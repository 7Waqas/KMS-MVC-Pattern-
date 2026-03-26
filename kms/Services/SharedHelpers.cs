namespace kms.Services
{
    public static class SharedHelpers
    {
        // Safely parse hour from time strings like "08:45", "8:45 AM", "08:45:00"
        public static int TryParseHour(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return -1;
            if (DateTime.TryParse(timeStr, out var dt)) return dt.Hour;
            if (TimeSpan.TryParse(timeStr, out var ts)) return ts.Hours;
            return -1;
        }
    }
}