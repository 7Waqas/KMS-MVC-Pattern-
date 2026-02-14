using System;
using System.Collections.Generic;

namespace kms.Models;

public partial class KeyAuthorization
{
    public int AuthId { get; set; }

    public int KeyEnroll { get; set; }

    public int EmpEnroll { get; set; }

    public DateOnly? AssignedDate { get; set; }
}
