namespace kms.Models.ViewModels
{
    public class EmployeeAuthViewModel
    {
        public string EmployeeName { get; set; }
        public int EmpEnroll { get; set; }
        public IEnumerable<KeyMaster> AvailableKeys { get; set; }
        public IEnumerable<int> AuthorizedKeyEnrolls { get; set; }
    }
}