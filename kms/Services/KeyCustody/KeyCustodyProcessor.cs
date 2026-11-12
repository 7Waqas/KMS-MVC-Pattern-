using kms.Models;
using kms.Models.KeyCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace kms.Services.KeyCustody;

/// <summary>
/// Reads the device scans in tmpTRecords and keeps KcEvent / KcIncident up to date.
///
/// Works per DAY: a day is (re)built whenever its scan count or highest ID changes. This handles
/// the attendance software inserting rows late (seen: rows arriving 5 days late) and inserting the
/// same scan twice (seen: 2,313 duplicate rows) without ever losing or duplicating anything.
/// Incidents the security person has already reviewed are never overwritten.
/// </summary>
public sealed class KeyCustodyProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly KeyCustodyOptions _o;
    private readonly ILogger<KeyCustodyProcessor> _log;

    public KeyCustodyProcessor(ApplicationDbContext db, IOptions<KeyCustodyOptions> o, ILogger<KeyCustodyProcessor> log)
    {
        _db = db;
        _o = o.Value;
        _log = log;
    }

    private sealed class DayStat
    {
        public DateTime Day { get; set; }
        public int RowsCount { get; set; }
        public int MaxId { get; set; }
        public int MaxHour { get; set; }
    }

    /// <summary>Processes every day that has new or changed scans. Returns the number of days rebuilt.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var stats = await _db.Database.SqlQueryRaw<DayStat>(
            @"SELECT CAST(CAST(KqDate AS date) AS datetime) AS Day, COUNT(*) AS RowsCount, MAX(ID) AS MaxId,
                     ISNULL(DATEPART(HOUR, MAX(CAST(KqTime AS time(0)))), 0) AS MaxHour
              FROM dbo.tmpTRecords
              WHERE KqDate IS NOT NULL
              GROUP BY CAST(KqDate AS date)").ToListAsync(ct);

        var processed = await _db.KcProcessedDays.AsNoTracking().ToDictionaryAsync(p => p.Day, ct);

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        ReferenceData? reference = null;
        int rebuilt = 0;

        var lastDataDay = stats.Count > 0 ? DateOnly.FromDateTime(stats.Max(s => s.Day)) : today;

        foreach (var s in stats.OrderBy(s => s.Day))
        {
            ct.ThrowIfCancellationRequested();
            var day = DateOnly.FromDateTime(s.Day);

            // A day is "over" (end-of-day checks run) when:
            //  - it is today and past SettleHour, or
            //  - it is in the past AND it is complete: scans exist on a later day, or the day's scans
            //    already reach SettleHour. This stops a backup taken mid-day (or a device that has not
            //    synced yet) from being treated as a finished day.
            bool settle = day == today
                ? now.Hour >= _o.SettleHour
                : day < today && (day < lastDataDay || s.MaxHour >= _o.SettleHour);

            if (processed.TryGetValue(day, out var p)
                && p.SourceRows == s.RowsCount && p.MaxSourceId == s.MaxId
                && p.Settled == settle)
                continue;

            reference ??= await LoadReferenceAsync(ct);
            await RebuildDayAsync(day, s, settle, reference, ct);
            rebuilt++;
        }

        if (rebuilt > 0)
            _log.LogInformation("Key custody: rebuilt {Days} day(s).", rebuilt);
        return rebuilt;
    }

    private async Task RebuildDayAsync(DateOnly day, DayStat stat, bool settle, ReferenceData r, CancellationToken ct)
    {
        var from = day.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);

        // De-duplicated scans of the day (same person/key at the same second counted once).
        var scans = await _db.Database.SqlQueryRaw<RawScan>(
            @"SELECT MIN(ID) AS SourceId,
                     TRY_CAST(emp_id AS int) AS Enroll,
                     CAST(CAST(KqDate AS date) AS datetime) + CAST(CAST(KqTime AS time(0)) AS datetime) AS Ts
              FROM dbo.tmpTRecords
              WHERE KqDate >= {0} AND KqDate < {1} AND TRY_CAST(emp_id AS int) IS NOT NULL
              GROUP BY TRY_CAST(emp_id AS int),
                       CAST(CAST(KqDate AS date) AS datetime) + CAST(CAST(KqTime AS time(0)) AS datetime)",
            from, to).ToListAsync(ct);

        var built = KeyCustodyEngine.BuildDay(day, scans, r, _o, settle);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Events: always rebuilt from scratch for the day.
        await _db.KcEvents.Where(e => e.ScanDate == day).ExecuteDeleteAsync(ct);
        _db.KcEvents.AddRange(built.Events);

        // Incidents: upsert by NaturalKey; reviewed ones are left untouched.
        var existing = await _db.KcIncidents.Include(i => i.Suspects)
            .Where(i => i.IncidentDate == day).ToListAsync(ct);
        var byKey = existing.ToDictionary(i => i.NaturalKey);
        var seen = new HashSet<string>();
        var now = DateTime.Now;

        foreach (var inc in built.Incidents)
        {
            if (!seen.Add(inc.NaturalKey)) continue;
            if (byKey.TryGetValue(inc.NaturalKey, out var old))
            {
                if (old.Status != KcStatus.Open) continue;   // keep the security person's decision
                old.Type = inc.Type;
                old.Severity = inc.Severity;
                old.KeyEnroll = inc.KeyEnroll;
                old.KeyName = inc.KeyName;
                old.OccurredAt = inc.OccurredAt;
                old.PersonEnroll = inc.PersonEnroll;
                old.PersonName = inc.PersonName;
                old.Summary = inc.Summary;
                old.AuthorizedPersons = inc.AuthorizedPersons;
                _db.KcIncidentSuspects.RemoveRange(old.Suspects);
                old.Suspects = inc.Suspects;
            }
            else
            {
                inc.CreatedAt = now;
                _db.KcIncidents.Add(inc);
            }
        }

        foreach (var old in existing.Where(i => i.Status == KcStatus.Open && !seen.Contains(i.NaturalKey)))
            _db.KcIncidents.Remove(old);   // no longer valid (e.g. a late face scan arrived)

        var pd = await _db.KcProcessedDays.FindAsync(new object[] { day }, ct);
        if (pd == null)
        {
            pd = new KcProcessedDay { Day = day };
            _db.KcProcessedDays.Add(pd);
        }
        pd.SourceRows = stat.RowsCount;
        pd.MaxSourceId = stat.MaxId;
        pd.Settled = settle;
        pd.ProcessedAt = now;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _db.ChangeTracker.Clear();
    }

    public async Task<ReferenceData> LoadReferenceAsync(CancellationToken ct)
    {
        var r = new ReferenceData();

        // EmployeeMaster has duplicate enroll numbers (55 found): prefer the active row.
        var emps = await _db.EmployeeMasters.AsNoTracking().ToListAsync(ct);
        foreach (var g in emps.GroupBy(e => e.EnrollNumber))
        {
            var e = g.OrderByDescending(x => x.IsActive).ThenBy(x => x.EmpId).First();
            r.People[g.Key] = new PersonInfo(g.Key, e.FullName, e.Department, e.IsActive);
        }

        var keys = await _db.KeyMasters.AsNoTracking().ToListAsync(ct);
        foreach (var g in keys.GroupBy(k => k.EnrollNumber))
        {
            var k = g.OrderByDescending(x => x.IsActive).ThenBy(x => x.KeyId).First();
            r.Keys[g.Key] = new KeyInfo(g.Key, k.KeyName, k.KeyLocation, k.IsActive);
        }

        var auths = await _db.KeyAuthorizations.AsNoTracking()
            .Select(a => new { a.KeyEnroll, a.EmpEnroll }).ToListAsync(ct);
        foreach (var a in auths)
        {
            if (!r.AuthByKey.TryGetValue(a.KeyEnroll, out var set))
                r.AuthByKey[a.KeyEnroll] = set = new HashSet<int>();
            set.Add(a.EmpEnroll);
        }
        return r;
    }
}

/// <summary>Runs the processor in the background for as long as KMS is running in IIS.</summary>
public sealed class KeyCustodyBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly KeyCustodyOptions _o;
    private readonly ILogger<KeyCustodyBackgroundService> _log;

    public KeyCustodyBackgroundService(IServiceScopeFactory scopes, IOptions<KeyCustodyOptions> o,
                                       ILogger<KeyCustodyBackgroundService> log)
    {
        _scopes = scopes;
        _o = o.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_o.Enabled)
        {
            _log.LogInformation("Key custody processor is disabled in configuration.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);   // let the app finish starting
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(Math.Max(3, _o.PollSeconds));
            try
            {
                using var scope = _scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<KeyCustodyProcessor>();
                await processor.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Key custody processing failed. Has Database/001_KeyCustody.sql been run?");
                delay = TimeSpan.FromMinutes(1);
            }
            await Task.Delay(delay, stoppingToken);
        }
    }
}
