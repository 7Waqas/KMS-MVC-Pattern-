using kms.Models.KeyCustody;

namespace kms.Services.KeyCustody;

/// <summary>One de-duplicated row from tmpTRecords.</summary>
public sealed class RawScan
{
    public int SourceId { get; set; }
    public int Enroll { get; set; }
    public DateTime Ts { get; set; }
}

public sealed record PersonInfo(int Enroll, string Name, string? Department, bool Active);
public sealed record KeyInfo(int Enroll, string Name, string? Location, bool Active);

/// <summary>Employees, keys and authorizations, loaded once per processing run.</summary>
public sealed class ReferenceData
{
    public Dictionary<int, PersonInfo> People { get; } = new();
    public Dictionary<int, KeyInfo> Keys { get; } = new();
    public Dictionary<int, HashSet<int>> AuthByKey { get; } = new();

    public static bool IsPersonEnroll(int enroll) => enroll is >= 1 and <= 1000;
    public static bool IsKeyEnroll(int enroll) => enroll >= 1001;

    /// <summary>Authorized = assigned to the key AND an active employee.</summary>
    public bool IsAuthorized(int keyEnroll, int empEnroll) =>
        AuthByKey.TryGetValue(keyEnroll, out var set) && set.Contains(empEnroll)
        && People.TryGetValue(empEnroll, out var p) && p.Active;

    public string PersonName(int enroll) =>
        People.TryGetValue(enroll, out var p) ? p.Name : $"Unknown person #{enroll}";

    public string KeyName(int enroll) =>
        Keys.TryGetValue(enroll, out var k) ? k.Name : $"Unregistered key #{enroll}";

    public string AuthorizedNames(int keyEnroll)
    {
        if (!AuthByKey.TryGetValue(keyEnroll, out var set)) return "--";
        var names = set.Where(e => People.TryGetValue(e, out var p) && p.Active)
                       .Select(PersonName).Distinct().OrderBy(n => n).ToList();
        var text = names.Count > 0 ? string.Join(", ", names) : "--";
        return text.Length > 500 ? text[..497] + "..." : text;
    }
}

public sealed class DayResult
{
    public List<KcEvent> Events { get; } = new();
    public List<KcIncident> Incidents { get; } = new();
}

/// <summary>
/// Turns one day of raw device scans into key events and security incidents.
///
/// Rules:
///  1. Every key scan is paired with the nearest face scan (±PairWindowSeconds), preferring a
///     person who is authorized for that key. The ACTUAL person is always recorded.
///  2. Per key, scans alternate OUT, IN, OUT, IN... during the day (no more guessing by 12:00).
///  3. At the end of the day an odd number of scans means one scan is missing:
///       - first scan after 12:00  → the key was taken WITHOUT scanning and later returned;
///       - otherwise               → the return was never scanned (custody stays with the taker).
///  4. Because the device in the key room is used only for keys, every face scan proves that
///     person was physically at the board — that list is used to rank suspects.
/// </summary>
public static class KeyCustodyEngine
{
    public static DayResult BuildDay(DateOnly day, IReadOnlyList<RawScan> scans, ReferenceData r,
                                     KeyCustodyOptions o, bool settle)
    {
        var result = new DayResult();
        var ordered = scans.OrderBy(s => s.Ts).ThenBy(s => s.SourceId).ToList();
        var faces = ordered.Where(s => ReferenceData.IsPersonEnroll(s.Enroll)).ToList();

        // ── Key scans, with accidental double taps removed ──
        var keyGroups = ordered.Where(s => ReferenceData.IsKeyEnroll(s.Enroll))
            .GroupBy(s => s.Enroll)
            .ToDictionary(g => g.Key, g =>
            {
                var kept = new List<RawScan>();
                foreach (var s in g)
                    if (kept.Count == 0 || (s.Ts - kept[^1].Ts).TotalSeconds > o.DoubleScanSeconds)
                        kept.Add(s);
                return kept;
            });

        foreach (var (keyEnroll, keyScans) in keyGroups)
        {
            var keyName = r.KeyName(keyEnroll);
            var authorizedNames = r.AuthorizedNames(keyEnroll);

            // Rule 3: odd count after the day is over and first scan in the afternoon
            // → the first scan was a RETURN of a key taken without scanning.
            bool firstIsReturn = settle && keyScans.Count % 2 == 1 && keyScans[0].Ts.Hour >= 12;

            var dayEvents = new List<KcEvent>();
            for (int i = 0; i < keyScans.Count; i++)
            {
                var k = keyScans[i];
                bool isOut = (i + (firstIsReturn ? 1 : 0)) % 2 == 0;
                var face = PairFace(k, faces, r, o.PairWindowSeconds);

                var ev = new KcEvent
                {
                    ScanDate = day,
                    ScanTime = k.Ts,
                    SourceId = k.SourceId,
                    KeyEnroll = keyEnroll,
                    KeyName = keyName,
                    Direction = isOut ? KcDirection.Out : KcDirection.In,
                    FaceEnroll = face?.Enroll,
                    FaceName = face != null ? r.PersonName(face.Enroll) : null,
                    FaceOffsetSec = face != null ? (int)(face.Ts - k.Ts).TotalSeconds : null,
                    Result = face == null ? KcResult.NoFace
                           : r.IsAuthorized(keyEnroll, face.Enroll) ? KcResult.Ok
                           : KcResult.NotAuthorized,
                    ProcessedAt = DateTime.Now
                };
                dayEvents.Add(ev);

                // ── Immediate incidents ──
                if (ev.Result == KcResult.NotAuthorized)
                {
                    result.Incidents.Add(new KcIncident
                    {
                        NaturalKey = $"{KcIncidentType.NotAuthorized}|{keyEnroll}|{k.Ts:yyyyMMddHHmmss}",
                        IncidentDate = day,
                        Type = KcIncidentType.NotAuthorized,
                        Severity = (byte)(isOut ? 1 : 2),
                        KeyEnroll = keyEnroll,
                        KeyName = keyName,
                        OccurredAt = k.Ts,
                        PersonEnroll = ev.FaceEnroll,
                        PersonName = ev.FaceName,
                        AuthorizedPersons = authorizedNames,
                        Summary = isOut
                            ? $"{ev.FaceName} took key {keyName} at {k.Ts:HH:mm:ss} but is NOT authorized for it."
                            : $"{ev.FaceName} returned key {keyName} at {k.Ts:HH:mm:ss} but is NOT authorized for it.",
                        Suspects =
                        {
                            Suspect(1, face!.Enroll, r, keyEnroll, "Face scanned with the key", face.Ts)
                        }
                    });
                }
                else if (ev.Result == KcResult.NoFace)
                {
                    var inc = new KcIncident
                    {
                        NaturalKey = $"{KcIncidentType.NoFace}|{keyEnroll}|{k.Ts:yyyyMMddHHmmss}",
                        IncidentDate = day,
                        Type = KcIncidentType.NoFace,
                        Severity = (byte)(isOut ? 1 : 2),
                        KeyEnroll = keyEnroll,
                        KeyName = keyName,
                        OccurredAt = k.Ts,
                        AuthorizedPersons = authorizedNames,
                        Summary = $"Key {keyName} was scanned {(isOut ? "OUT" : "IN")} at {k.Ts:HH:mm:ss} with NO face scan."
                    };
                    AddPresenceSuspects(inc, keyEnroll, faces, r,
                        from: k.Ts.AddSeconds(-o.PresenceWindowSeconds),
                        to: k.Ts.AddSeconds(o.PresenceWindowSeconds),
                        reference: k.Ts);
                    var top = inc.Suspects.FirstOrDefault();
                    if (top != null) { inc.PersonEnroll = top.EmpEnroll; inc.PersonName = top.EmpName; }
                    result.Incidents.Add(inc);
                }
            }
            result.Events.AddRange(dayEvents);

            // ── End-of-day custody checks ──
            if (!settle || keyScans.Count % 2 == 0) continue;

            if (firstIsReturn)
            {
                var ret = dayEvents[0];
                var inc = new KcIncident
                {
                    NaturalKey = $"{KcIncidentType.TakenWithoutScan}|{keyEnroll}|{day:yyyyMMdd}",
                    IncidentDate = day,
                    Type = KcIncidentType.TakenWithoutScan,
                    Severity = 1,
                    KeyEnroll = keyEnroll,
                    KeyName = keyName,
                    OccurredAt = ret.ScanTime,
                    AuthorizedPersons = authorizedNames,
                    Summary = $"Key {keyName} was scanned back IN at {ret.ScanTime:HH:mm:ss}"
                            + (ret.FaceName != null ? $" by {ret.FaceName}" : "")
                            + " but was never scanned OUT — it was taken without scanning."
                };
                int rank = 1;
                if (ret.FaceEnroll is int retEnroll)
                    inc.Suspects.Add(Suspect(rank++, retEnroll, r, keyEnroll,
                        $"Returned the key at {ret.ScanTime:HH:mm}", ret.ScanTime));
                AddPresenceSuspects(inc, keyEnroll, faces, r,
                    from: day.ToDateTime(TimeOnly.MinValue), to: ret.ScanTime, reference: null,
                    startRank: rank, maxCount: 15);
                var top = inc.Suspects.FirstOrDefault();
                if (top != null) { inc.PersonEnroll = top.EmpEnroll; inc.PersonName = top.EmpName; }
                result.Incidents.Add(inc);
            }
            else
            {
                var lastOut = dayEvents[^1];
                var inc = new KcIncident
                {
                    NaturalKey = $"{KcIncidentType.ReturnNotScanned}|{keyEnroll}|{day:yyyyMMdd}",
                    IncidentDate = day,
                    Type = KcIncidentType.ReturnNotScanned,
                    Severity = 2,
                    KeyEnroll = keyEnroll,
                    KeyName = keyName,
                    OccurredAt = lastOut.ScanTime,
                    PersonEnroll = lastOut.FaceEnroll,
                    PersonName = lastOut.FaceName,
                    AuthorizedPersons = authorizedNames,
                    Summary = $"Key {keyName} was taken at {lastOut.ScanTime:HH:mm:ss}"
                            + (lastOut.FaceName != null ? $" by {lastOut.FaceName}" : " (no face scanned)")
                            + " and its return was never scanned."
                };
                if (lastOut.FaceEnroll is int holder)
                    inc.Suspects.Add(Suspect(1, holder, r, keyEnroll,
                        $"Took the key at {lastOut.ScanTime:HH:mm}; no return scan", lastOut.ScanTime));
                else
                    AddPresenceSuspects(inc, keyEnroll, faces, r,
                        from: lastOut.ScanTime.AddSeconds(-o.PresenceWindowSeconds),
                        to: lastOut.ScanTime.AddSeconds(o.PresenceWindowSeconds),
                        reference: lastOut.ScanTime);
                result.Incidents.Add(inc);
            }
        }

        // ── Silent keys: authorized holders were physically at the board, key never scanned ──
        if (settle && o.DetectSilentKeys)
        {
            var presentFirstSeen = faces.GroupBy(f => f.Enroll).ToDictionary(g => g.Key, g => g.Min(x => x.Ts));
            foreach (var key in r.Keys.Values.Where(k => k.Active && !keyGroups.ContainsKey(k.Enroll)))
            {
                if (!r.AuthByKey.TryGetValue(key.Enroll, out var holders)) continue;
                var present = holders.Where(h => presentFirstSeen.ContainsKey(h) && r.IsAuthorized(key.Enroll, h))
                                     .OrderBy(h => presentFirstSeen[h]).ToList();
                if (present.Count == 0) continue;

                var inc = new KcIncident
                {
                    NaturalKey = $"{KcIncidentType.SilentKey}|{key.Enroll}|{day:yyyyMMdd}",
                    IncidentDate = day,
                    Type = KcIncidentType.SilentKey,
                    Severity = 3,
                    KeyEnroll = key.Enroll,
                    KeyName = key.Name,
                    AuthorizedPersons = r.AuthorizedNames(key.Enroll),
                    Summary = $"{present.Count} authorized holder(s) of {key.Name} were at the key board, "
                            + "but the key was never scanned all day."
                };
                int rank = 1;
                foreach (var h in present)
                    inc.Suspects.Add(Suspect(rank++, h, r, key.Enroll,
                        $"Authorized; at the board at {presentFirstSeen[h]:HH:mm}", presentFirstSeen[h]));
                result.Incidents.Add(inc);
            }
        }

        return result;
    }

    /// <summary>Nearest face within the window, preferring someone authorized for the key.</summary>
    private static RawScan? PairFace(RawScan key, List<RawScan> faces, ReferenceData r, int windowSec)
    {
        var from = key.Ts.AddSeconds(-windowSec);
        var to = key.Ts.AddSeconds(windowSec);
        RawScan? bestAuth = null, bestAny = null;
        double bestAuthDist = double.MaxValue, bestAnyDist = double.MaxValue;
        foreach (var f in faces)
        {
            if (f.Ts < from) continue;
            if (f.Ts > to) break;
            var dist = Math.Abs((f.Ts - key.Ts).TotalSeconds);
            if (dist < bestAnyDist) { bestAnyDist = dist; bestAny = f; }
            if (dist < bestAuthDist && r.IsAuthorized(key.Enroll, f.Enroll)) { bestAuthDist = dist; bestAuth = f; }
        }
        return bestAuth ?? bestAny;
    }

    /// <summary>Adds everyone whose face was scanned in [from, to], authorized people first.</summary>
    private static void AddPresenceSuspects(KcIncident inc, int keyEnroll, List<RawScan> faces, ReferenceData r,
                                            DateTime from, DateTime to, DateTime? reference,
                                            int startRank = 1, int maxCount = 10)
    {
        var already = inc.Suspects.Select(s => s.EmpEnroll).ToHashSet();
        var people = faces.Where(f => f.Ts >= from && f.Ts <= to && !already.Contains(f.Enroll))
            .GroupBy(f => f.Enroll)
            .Select(g =>
            {
                // the sighting closest to the reference time (or the last one before 'to')
                var seen = reference is DateTime rt
                    ? g.OrderBy(x => Math.Abs((x.Ts - rt).TotalSeconds)).First().Ts
                    : g.Max(x => x.Ts);
                return new { Enroll = g.Key, Seen = seen, Auth = r.IsAuthorized(keyEnroll, g.Key) };
            })
            .OrderByDescending(x => x.Auth)
            .ThenBy(x => reference is DateTime rt ? Math.Abs((x.Seen - rt).TotalSeconds) : (to - x.Seen).TotalSeconds)
            .Take(maxCount);

        int rank = startRank;
        foreach (var p in people)
        {
            string when = reference is DateTime rt
                ? $"{(int)(p.Seen - rt).TotalSeconds:+0;-0;0}s from the key scan"
                : $"at {p.Seen:HH:mm}";
            inc.Suspects.Add(Suspect(rank++, p.Enroll, r, keyEnroll, $"At the board {when}", p.Seen));
        }
    }

    private static KcIncidentSuspect Suspect(int rank, int enroll, ReferenceData r, int keyEnroll,
                                             string reason, DateTime? seen)
    {
        bool auth = r.IsAuthorized(keyEnroll, enroll);
        var fullReason = reason + (auth ? " · authorized for this key" : " · NOT authorized");
        return new KcIncidentSuspect
        {
            Rank = rank,
            EmpEnroll = enroll,
            EmpName = r.PersonName(enroll),
            IsAuthorized = auth,
            Reason = fullReason.Length > 200 ? fullReason[..200] : fullReason,
            SeenAt = seen
        };
    }
}
