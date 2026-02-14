namespace kms.Models.ViewModels
{
    public class KeyAuthViewModel
    {
        public int KeyId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public List<int> AuthorizedEmployeeIds { get; set; } = new();
        public List<EmployeeMaster> AvailableEmployees { get; set; } = new();
        public List<EmployeeMaster> CurrentAuthorizedEmployees { get; set; } = new();
    }
}