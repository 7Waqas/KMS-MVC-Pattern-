using System;
using System.Collections.Generic;

namespace kms.Models;

public partial class KeyMaster
{
    public int KeyId { get; set; }

    public int EnrollNumber { get; set; }

    public string KeyName { get; set; } = null!;

    public string? KeyLocation { get; set; }

    public bool? IsActive { get; set; }
}
