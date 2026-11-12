using kms.Models;
using kms.Models.KeyCustody;
using kms.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace kms.Services.KeyCustody;

/// <summary>
/// Read-side queries shared by the Dashboard and the Security screens, so every page counts
/// keys the same way (single source of truth: KcEvent / KcIncident).
/// </summary>
public sealed class KeyCustodyQueries
{
    private readonly ApplicationDbContext _db;
    private readonly KeyCustodyOptions _o;

    public KeyCustodyQueries(ApplicationDbContext db, IOptions<KeyCustodyOptions> o)
    {
        _db = db;
        _o = o.Value;
    }

    public async Task<DateOnly?> LatestDataDateAsync() =>
        await _db.KcEvents.AnyAsync() ? await _db.KcEvents.MaxAsync(e => e.ScanDate) : null;

    /// <summary>Requested date, or today; on a laptop copy with no scans today, the last day with data.</summary>
    public async Task<DateOnly> ResolveDateAsync(DateOnly? requested)
    {
        if (requested != null) return requested.Value;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (await _db.KcEvents.AnyAsync(e => e.ScanDate == today)) return today;
        return await LatestDataDateAsync() ?? today;
    }

    /// <summary>State of every active key at the end of the given day (or now, for today).</summary>
    public async Task<SecurityBoardViewModel> BuildBoardAsync(DateOnly d)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        bool isToday = d == today;
        bool afterOverdue = isToday && DateTime.Now.Hour >= _o.OverdueHour;
        bool dayOver = !isToday && await _db.KcProcessedDays.AnyAsync(p => p.Day == d && p.Settled);

        var keys = await _db.KeyMasters.AsNoTracking().Where(k => k.IsActive).OrderBy(k => k.KeyName).ToListAsync();
        var events = await _db.KcEvents.AsNoTracking().Where(e => e.ScanDate == d).OrderBy(e => e.ScanTime).ToListAsync();
        var byKey = events.GroupBy(e => e.KeyEnroll).ToDictionary(g => g.Key, g => g.ToList());

        var tiles = new List<KeyTile>();
        foreach (var k in keys.GroupBy(k => k.EnrollNumber).Select(g => g.First()))
        {
            var tile = new KeyTile { KeyEnroll = k.EnrollNumber, KeyName = k.KeyName, Location = k.KeyLocation };
            if (byKey.TryGetValue(k.EnrollNumber, out var evs) && evs.Count > 0)
            {
                var last = evs[^1];
                tile.MovesToday = evs.Count;
                tile.Since = last.ScanTime;
                tile.LastResult = last.Result;
                tile.LastEventId = last.Id;
                tile.HolderEnroll = last.FaceEnroll;
                if (last.Direction == KcDirection.Out)
                {
                    tile.Holder = last.FaceName ?? "Unknown (no face)";
                    tile.State = dayOver ? KeyTileState.NotReturned
                               : afterOverdue ? KeyTileState.Overdue
                               : last.Result == KcResult.Ok ? KeyTileState.Out
                               : KeyTileState.OutProblem;
                }
                else
                {
                    tile.Holder = last.FaceName;
                    tile.State = KeyTileState.Returned;
                }
            }
            else tile.State = KeyTileState.OnBoard;
            tiles.Add(tile);
        }

        return new SecurityBoardViewModel
        {
            Date = d,
            IsToday = isToday,
            LatestDataDate = await LatestDataDateAsync(),
            Keys = tiles,
            Incidents = await _db.KcIncidents.AsNoTracking()
                .Where(i => i.IncidentDate == d && i.Severity <= 2)
                .OrderByDescending(i => i.OccurredAt).Take(100).ToListAsync()
        };
    }

    /// <summary>Board group used by filters: attention / out / returned / board.</summary>
    public static string Group(KeyTile k) => k.State switch
    {
        KeyTileState.OutProblem or KeyTileState.Overdue or KeyTileState.NotReturned => "attention",
        KeyTileState.Out => "out",
        KeyTileState.Returned => "returned",
        _ => "board"
    };

    /// <summary>Everyone whose face was scanned at the board in [from, to] (device is used only for keys).</summary>
    public async Task<List<PersonSighting>> PeopleAtBoardAsync(DateTime from, DateTime to, int? keyEnroll = null)
    {
        var rows = await _db.Database.SqlQueryRaw<RawScan>(
            @"SELECT MIN(ID) AS SourceId,
                     TRY_CAST(emp_id AS int) AS Enroll,
                     CAST(CAST(KqDate AS date) AS datetime) + CAST(CAST(KqTime AS time(0)) AS datetime) AS Ts
              FROM dbo.tmpTRecords
              WHERE KqDate >= {0} AND KqDate < {1}
                AND TRY_CAST(emp_id AS int) BETWEEN 1 AND 1000
              GROUP BY TRY_CAST(emp_id AS int),
                       CAST(CAST(KqDate AS date) AS datetime) + CAST(CAST(KqTime AS time(0)) AS datetime)",
            from.Date, to.Date.AddDays(1)).ToListAsync();

        rows = rows.Where(r => r.Ts >= from && r.Ts <= to).ToList();
        if (rows.Count == 0) return new();

        var enrolls = rows.Select(r => r.Enroll).Distinct().ToList();
        var people = (await _db.EmployeeMasters.AsNoTracking().Where(e => enrolls.Contains(e.EnrollNumber)).ToListAsync())
            .GroupBy(e => e.EnrollNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsActive).First());
        var authorized = keyEnroll == null ? new HashSet<int>()
            : (await _db.KeyAuthorizations.AsNoTracking().Where(a => a.KeyEnroll == keyEnroll)
                  .Select(a => a.EmpEnroll).ToListAsync()).ToHashSet();

        return rows.OrderBy(r => r.Ts).Select(r => new PersonSighting
        {
            Enroll = r.Enroll,
            Name = people.TryGetValue(r.Enroll, out var p) ? p.FullName : $"Unknown person #{r.Enroll}",
            Department = people.TryGetValue(r.Enroll, out var p2) ? p2.Department : null,
            At = r.Ts,
            IsAuthorized = authorized.Contains(r.Enroll) && people.TryGetValue(r.Enroll, out var p3) && p3.IsActive
        }).ToList();
    }

    /// <summary>Active employees authorized for a key.</summary>
    public async Task<List<PersonSighting>> AuthorizedHoldersAsync(int keyEnroll)
    {
        var emp = await _db.KeyAuthorizations.AsNoTracking().Where(a => a.KeyEnroll == keyEnroll)
            .Select(a => a.EmpEnroll).Distinct().ToListAsync();
        var people = await _db.EmployeeMasters.AsNoTracking()
            .Where(e => emp.Contains(e.EnrollNumber) && e.IsActive).ToListAsync();
        return people.GroupBy(p => p.EnrollNumber).Select(g => g.First())
            .OrderBy(p => p.FullName)
            .Select(p => new PersonSighting { Enroll = p.EnrollNumber, Name = p.FullName, Department = p.Department, IsAuthorized = true })
            .ToList();
    }
}
