using System;
using System.Collections.Generic;

namespace kms.Models;

public partial class KeyReportData
{
    public int Id { get; set; }

    public int ReportType { get; set; }

    public DateOnly ReportDate { get; set; }

    public string? KeyName { get; set; }

    public string? Status { get; set; }

    public string? ScanTime { get; set; }

    public string? Employee { get; set; }

    public string? AuthorizedPersons { get; set; }

    public string? AlertStatus { get; set; }

    public string? Direction { get; set; }

    public string? AuthStatus { get; set; }
}
