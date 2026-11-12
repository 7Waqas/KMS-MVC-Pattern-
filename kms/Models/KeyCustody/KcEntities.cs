namespace kms.Models.KeyCustody;

public static class KcDirection
{
    public const string Out = "OUT";
    public const string In = "IN";
}

public static class KcResult
{
    public const string Ok = "OK";
    public const string NotAuthorized = "NOT_AUTHORIZED";
    public const string NoFace = "NO_FACE";
}

public static class KcIncidentType
{
    /// <summary>Key scanned together with the face of someone not authorized for it.</summary>
    public const string NotAuthorized = "NOT_AUTHORIZED";
    /// <summary>Key card scanned, but nobody's face was scanned with it.</summary>
    public const string NoFace = "NO_FACE";
    /// <summary>Key came back (scanned in) but was never scanned out — it was taken without scanning.</summary>
    public const string TakenWithoutScan = "TAKEN_WITHOUT_SCAN";
    /// <summary>Key was scanned out but its return was never scanned.</summary>
    public const string ReturnNotScanned = "RETURN_NOT_SCANNED";
    /// <summary>Authorized holders were at the board but the key was never scanned that day.</summary>
    public const string SilentKey = "SILENT_KEY";

    public static string Label(string type) => type switch
    {
        NotAuthorized => "Wrong person",
        NoFace => "No face scanned",
        TakenWithoutScan => "Taken without any scan",
        ReturnNotScanned => "Return not scanned",
        SilentKey => "Key never scanned",
        _ => type
    };
}

public static class KcStatus
{
    public const string Open = "Open";
    public const string Confirmed = "Confirmed";
    public const string Dismissed = "Dismissed";
}

public class KcEvent
{
    public int Id { get; set; }
    public DateOnly ScanDate { get; set; }
    public DateTime ScanTime { get; set; }
    public int SourceId { get; set; }
    public int KeyEnroll { get; set; }
    public string KeyName { get; set; } = "";
    public string Direction { get; set; } = KcDirection.Out;
    public string Result { get; set; } = KcResult.Ok;
    public int? FaceEnroll { get; set; }
    public string? FaceName { get; set; }
    public int? FaceOffsetSec { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public class KcIncident
{
    public int Id { get; set; }
    public string NaturalKey { get; set; } = "";
    public DateOnly IncidentDate { get; set; }
    public string Type { get; set; } = "";
    public byte Severity { get; set; }
    public int KeyEnroll { get; set; }
    public string KeyName { get; set; } = "";
    public DateTime? OccurredAt { get; set; }
    public int? PersonEnroll { get; set; }
    public string? PersonName { get; set; }
    public string Summary { get; set; } = "";
    public string? AuthorizedPersons { get; set; }
    public string Status { get; set; } = KcStatus.Open;
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<KcIncidentSuspect> Suspects { get; set; } = new();
}

public class KcIncidentSuspect
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int Rank { get; set; }
    public int EmpEnroll { get; set; }
    public string EmpName { get; set; } = "";
    public bool IsAuthorized { get; set; }
    public string Reason { get; set; } = "";
    public DateTime? SeenAt { get; set; }
}

public class KcProcessedDay
{
    public DateOnly Day { get; set; }
    public int SourceRows { get; set; }
    public int MaxSourceId { get; set; }
    public bool Settled { get; set; }
    public DateTime ProcessedAt { get; set; }
}
