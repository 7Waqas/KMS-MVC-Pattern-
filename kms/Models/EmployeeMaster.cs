using System;
using System.Collections.Generic;

namespace kms.Models;

public partial class EmployeeMaster
{
    public int EmpId { get; set; }

    public int EnrollNumber { get; set; }

    public string FullName { get; set; } = null!;

    public string? Department { get; set; }

    public bool? IsActive { get; set; }
}
