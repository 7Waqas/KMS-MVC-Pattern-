using kms.Models.KeyCustody;

namespace kms.Models.ViewModels;

public enum KeyTileState { OnBoard, Returned, Out, OutProblem, Overdue, NotReturned }

public sealed class KeyTile
{
    public int KeyEnroll { get; set; }
    public string KeyName { get; set; } = "";
    public string? Location { get; set; }
    public KeyTileState State { get; set; }
    public string? Holder { get; set; }
    public int? HolderEnroll { get; set; }
    public DateTime? Since { get; set; }
    public string? LastResult { get; set; }
    public int? LastEventId { get; set; }
    public int MovesToday { get; set; }
}

public sealed class SecurityBoardViewModel
{
    public DateOnly Date { get; set; }
    public bool IsToday { get; set; }
    public DateOnly? LatestDataDate { get; set; }
    public string Filter { get; set; } = "all";
    public List<KeyTile> Keys { get; set; } = new();
    public List<KcIncident> Incidents { get; set; } = new();
}

public sealed class PersonSighting
{
    public int Enroll { get; set; }
    public string Name { get; set; } = "";
    public string? Department { get; set; }
    public DateTime? At { get; set; }
    public bool IsAuthorized { get; set; }
}

/// <summary>Dashboard: one day at a glance, every number links to its detail list.</summary>
public sealed class HomeDashboardViewModel
{
    public SecurityBoardViewModel Board { get; set; } = new();
    public int TotalEmployees { get; set; }
    public int ScansTotal { get; set; }
    public int ScansOk { get; set; }
    public int ScansNoFace { get; set; }
    public int ScansWrongPerson { get; set; }
    public int IncidentsOpen { get; set; }
    public int[] HourOut { get; set; } = new int[24];
    public int[] HourIn { get; set; } = new int[24];
    public int[] HourProblem { get; set; } = new int[24];
    public List<KcEvent> Latest { get; set; } = new();


    // NEW 6 — closing tracker
    public int ClosingHour { get; set; }
}

public sealed class KeyLogViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Result { get; set; }
    public string? Direction { get; set; }
    public int? Hour { get; set; }
    public string? Search { get; set; }
    public int? KeyEnroll { get; set; }
    public int TotalMatching { get; set; }
    public int CountOk { get; set; }
    public int CountNoFace { get; set; }
    public int CountWrongPerson { get; set; }
    public List<KcEvent> Items { get; set; } = new();
}

public sealed class MovementViewModel
{
    public KcEvent Event { get; set; } = null!;
    public string? KeyLocation { get; set; }
    public List<PersonSighting> Authorized { get; set; } = new();
    public List<PersonSighting> PeopleAtBoard { get; set; } = new();
    public List<KcEvent> KeyDay { get; set; } = new();
    public List<KcIncident> Incidents { get; set; } = new();
}

public sealed class KeyDetailViewModel
{
    public int KeyEnroll { get; set; }
    public string KeyName { get; set; } = "";
    public string? Location { get; set; }
    public DateOnly Date { get; set; }
    public KeyTile Now { get; set; } = new();
    public List<PersonSighting> Authorized { get; set; } = new();
    public List<KcEvent> Day { get; set; } = new();
    public List<KcIncident> RecentIncidents { get; set; } = new();
    public List<(DateOnly Day, int Moves, int Problems)> History { get; set; } = new();
}

public sealed class IncidentListViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public int MaxSeverity { get; set; }
    public string? Search { get; set; }
    public List<KcIncident> Items { get; set; } = new();
    public int TotalMatching { get; set; }
}

public sealed class IncidentDetailViewModel
{
    public KcIncident Incident { get; set; } = null!;
    public List<KcEvent> KeyTimeline { get; set; } = new();
}

public sealed class AuditPersonRow
{
    public int Enroll { get; set; }
    public string Name { get; set; } = "";
    public string? Department { get; set; }
    public int Total { get; set; }
    public int NotAuthorized { get; set; }
    public int NoFace { get; set; }
    public int TakenWithoutScan { get; set; }
    public int ReturnNotScanned { get; set; }
}

public sealed class AuditCountRow
{
    public string Name { get; set; } = "";
    public int? KeyEnroll { get; set; }
    public int Count { get; set; }
}

public sealed class AuditViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int KeyScans { get; set; }
    public int ScansOk { get; set; }
    public int ScansNoFace { get; set; }
    public int ScansNotAuthorized { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public List<AuditPersonRow> People { get; set; } = new();
    public List<AuditCountRow> Keys { get; set; } = new();
    public List<AuditCountRow> Departments { get; set; } = new();
    public int Confirmed { get; set; }
    public int Dismissed { get; set; }
}

/// <summary>Daily Security Report (one A4 page for the security officer / his boss).</summary>
public sealed class DailyReportViewModel
{
    public DateOnly Date { get; set; }
    public bool IsToday { get; set; }
    public bool DayOver { get; set; }
    public DateTime PrintedAt { get; set; }
    public string PrintedBy { get; set; } = "";
    public bool AutoPrint { get; set; }
    public int KeysTaken { get; set; }
    public int KeysReturned { get; set; }
    public int KeysNotMoved { get; set; }
    public int ScansTotal { get; set; }
    public int ScansOk { get; set; }
    public int ScansWrongPerson { get; set; }
    public int ScansNoFace { get; set; }
    public List<KcIncident> Problems { get; set; } = new();
    public List<KeyTile> StillOut { get; set; } = new();
}

/// <summary>Printable key list: keys still out (for collecting at closing) or keys not taken.</summary>
public sealed class KeyListReportViewModel
{
    public DateOnly Date { get; set; }
    public bool IsToday { get; set; }
    public string List { get; set; } = "out";   // out | nottaken
    public DateTime PrintedAt { get; set; }
    public string PrintedBy { get; set; } = "";
    public bool AutoPrint { get; set; }
    public List<KeyTile> Keys { get; set; } = new();
    public Dictionary<int, string> AuthorizedNames { get; set; } = new();
}
