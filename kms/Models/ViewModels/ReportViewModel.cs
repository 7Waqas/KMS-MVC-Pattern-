namespace kms.Models.ViewModels
{
    public class ReportViewModel
    {
        public DateOnly SelectedDate { get; set; }
        public int ReportType { get; set; }
        public string ReportTitle { get; set; } = string.Empty;
        public List<Dictionary<string, object>> ReportData { get; set; } = new();
        public int TotalRecords { get; set; }
    }
}