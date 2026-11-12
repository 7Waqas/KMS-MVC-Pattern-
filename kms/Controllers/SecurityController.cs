using kms.Models;
using kms.Models.KeyCustody;
using kms.Models.ViewModels;
using kms.Services.KeyCustody;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kms.Controllers
{
    /// <summary>Key Custody screens for the security person. Every list links to a detail page.</summary>
    [Authorize]
    public class SecurityController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly KeyCustodyQueries _q;

        public SecurityController(ApplicationDbContext db, KeyCustodyQueries q)
        {
            _db = db;
            _q = q;
        }

        // =============================================
        // KEY BOARD: every key as a coloured box
        // =============================================
        public async Task<IActionResult> Index(DateOnly? date, string? filter)
        {
            var d = await _q.ResolveDateAsync(date);
            var vm = await _q.BuildBoardAsync(d);
            vm.Filter = filter is "attention" or "out" or "returned" or "board" or "notreturned" ? filter : "all";
            return View(vm);
        }

        // =============================================
        // KEY LOG: every key scan, OK or not
        // =============================================
        public async Task<IActionResult> Log(DateOnly? date, DateOnly? from, DateOnly? to, string? result,
                                             string? direction, int? hour, int? key, string? q)
        {
            // one day (date=) or a range (from= & to=); default: today / last day with data
            var f = from ?? await _q.ResolveDateAsync(date);
            var t = to ?? f;
            if (t < f) (f, t) = (t, f);

            var baseQuery = _db.KcEvents.AsNoTracking().Where(e => e.ScanDate >= f && e.ScanDate <= t);
            if (key != null) baseQuery = baseQuery.Where(e => e.KeyEnroll == key);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim();
                baseQuery = baseQuery.Where(e => e.KeyName.Contains(s) || (e.FaceName != null && e.FaceName.Contains(s)));
            }
            if (!string.IsNullOrEmpty(direction)) baseQuery = baseQuery.Where(e => e.Direction == direction);
            if (hour != null) baseQuery = baseQuery.Where(e => e.ScanTime.Hour == hour);

            var counts = await baseQuery.GroupBy(e => e.Result).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var query = string.IsNullOrEmpty(result) ? baseQuery : baseQuery.Where(e => e.Result == result);

            var vm = new KeyLogViewModel
            {
                From = f, To = t, Result = result, Direction = direction, Hour = hour, Search = q, KeyEnroll = key,
                CountOk = counts.Where(c => c.Key == KcResult.Ok).Sum(c => c.N),
                CountNoFace = counts.Where(c => c.Key == KcResult.NoFace).Sum(c => c.N),
                CountWrongPerson = counts.Where(c => c.Key == KcResult.NotAuthorized).Sum(c => c.N),
                TotalMatching = await query.CountAsync(),
                Items = await query.OrderByDescending(e => e.ScanTime).Take(500).ToListAsync()
            };
            return View(vm);
        }

        // =============================================
        // MOVEMENT: one key scan in full detail (OK ones too)
        // =============================================
        public async Task<IActionResult> Movement(int id)
        {
            var ev = await _db.KcEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            var key = await _db.KeyMasters.AsNoTracking().FirstOrDefaultAsync(k => k.EnrollNumber == ev.KeyEnroll);
            var stamp = ev.ScanTime.ToString("yyyyMMddHHmmss");
            var vm = new MovementViewModel
            {
                Event = ev,
                KeyLocation = key?.KeyLocation,
                Authorized = await _q.AuthorizedHoldersAsync(ev.KeyEnroll),
                PeopleAtBoard = await SafePeopleAsync(ev.ScanTime.AddMinutes(-3), ev.ScanTime.AddMinutes(3), ev.KeyEnroll),
                KeyDay = await _db.KcEvents.AsNoTracking()
                    .Where(e => e.ScanDate == ev.ScanDate && e.KeyEnroll == ev.KeyEnroll)
                    .OrderBy(e => e.ScanTime).ToListAsync(),
                Incidents = await _db.KcIncidents.AsNoTracking()
                    .Where(i => i.IncidentDate == ev.ScanDate && i.KeyEnroll == ev.KeyEnroll
                                && (i.NaturalKey.EndsWith("|" + stamp) || i.OccurredAt == ev.ScanTime))
                    .ToListAsync()
            };
            return View(vm);
        }

        // =============================================
        // KEY: one key — who may take it, its day, its history
        // =============================================
        public async Task<IActionResult> Key(int id, DateOnly? date)
        {
            var key = await _db.KeyMasters.AsNoTracking().FirstOrDefaultAsync(k => k.EnrollNumber == id);
            if (key == null) return NotFound();
            var d = await _q.ResolveDateAsync(date);

            var board = await _q.BuildBoardAsync(d);
            var since = d.AddDays(-13);
            var history = await _db.KcEvents.AsNoTracking()
                .Where(e => e.KeyEnroll == id && e.ScanDate >= since && e.ScanDate <= d)
                .GroupBy(e => e.ScanDate)
                .Select(g => new { Day = g.Key, Moves = g.Count(), Problems = g.Count(x => x.Result != KcResult.Ok) })
                .ToListAsync();

            var vm = new KeyDetailViewModel
            {
                KeyEnroll = id,
                KeyName = key.KeyName,
                Location = key.KeyLocation,
                Date = d,
                Now = board.Keys.FirstOrDefault(k => k.KeyEnroll == id) ?? new KeyTile { KeyEnroll = id, KeyName = key.KeyName },
                Authorized = await _q.AuthorizedHoldersAsync(id),
                Day = await _db.KcEvents.AsNoTracking().Where(e => e.KeyEnroll == id && e.ScanDate == d)
                    .OrderBy(e => e.ScanTime).ToListAsync(),
                RecentIncidents = await _db.KcIncidents.AsNoTracking()
                    .Where(i => i.KeyEnroll == id && i.Severity <= 2)
                    .OrderByDescending(i => i.IncidentDate).ThenByDescending(i => i.OccurredAt).Take(15).ToListAsync(),
                History = Enumerable.Range(0, 14).Select(n => d.AddDays(-n))
                    .Select(day =>
                    {
                        var h = history.FirstOrDefault(x => x.Day == day);
                        return (day, h?.Moves ?? 0, h?.Problems ?? 0);
                    }).ToList()
            };
            return View(vm);
        }

        private async Task<List<PersonSighting>> SafePeopleAsync(DateTime from, DateTime to, int keyEnroll)
        {
            try { return await _q.PeopleAtBoardAsync(from, to, keyEnroll); }
            catch { return new(); }   // mock (in-memory) mode has no tmpTRecords
        }

        // =============================================
        // INCIDENTS
        // =============================================
        public async Task<IActionResult> Incidents(DateOnly? from, DateOnly? to, string? type,
                                                   string? status, int maxSeverity = 2, string? q = null)
        {
            var latest = await _q.ResolveDateAsync(null);
            var t = to ?? latest;
            var f = from ?? t.AddDays(-6);
            if (t < f) (f, t) = (t, f);

            var query = _db.KcIncidents.AsNoTracking()
                .Where(i => i.IncidentDate >= f && i.IncidentDate <= t && i.Severity <= maxSeverity);
            if (!string.IsNullOrEmpty(type)) query = query.Where(i => i.Type == type);
            if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim();
                query = query.Where(i => i.KeyName.Contains(s) || (i.PersonName != null && i.PersonName.Contains(s)));
            }

            var vm = new IncidentListViewModel
            {
                From = f, To = t, Type = type, Status = status, MaxSeverity = maxSeverity, Search = q,
                TotalMatching = await query.CountAsync(),
                Items = await query.OrderByDescending(i => i.IncidentDate).ThenByDescending(i => i.OccurredAt)
                                   .Take(500).ToListAsync()
            };
            return View(vm);
        }

        public async Task<IActionResult> Incident(int id)
        {
            var inc = await _db.KcIncidents.AsNoTracking().Include(i => i.Suspects)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inc == null) return NotFound();
            inc.Suspects = inc.Suspects.OrderBy(s => s.Rank).ToList();

            var vm = new IncidentDetailViewModel
            {
                Incident = inc,
                KeyTimeline = await _db.KcEvents.AsNoTracking()
                    .Where(e => e.ScanDate == inc.IncidentDate && e.KeyEnroll == inc.KeyEnroll)
                    .OrderBy(e => e.ScanTime).ToListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, string status, string? note)
        {
            if (status != KcStatus.Open && status != KcStatus.Confirmed && status != KcStatus.Dismissed)
                return BadRequest();
            var inc = await _db.KcIncidents.FindAsync(id);
            if (inc == null) return NotFound();

            var trimmed = note?.Trim();
            inc.Status = status;
            inc.ReviewNote = string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 500)];
            inc.ReviewedAt = status == KcStatus.Open ? null : DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Incident marked as {status}.";
            return RedirectToAction(nameof(Incident), new { id });
        }

        // =============================================
        // AUDIT REPORT (for management)
        // =============================================
        public async Task<IActionResult> Audit(DateOnly? from, DateOnly? to)
        {
            var hasData = await _db.KcEvents.AnyAsync();
            var f = from ?? (hasData ? await _db.KcEvents.MinAsync(e => e.ScanDate) : DateOnly.FromDateTime(DateTime.Today));
            var t = to ?? (hasData ? await _db.KcEvents.MaxAsync(e => e.ScanDate) : DateOnly.FromDateTime(DateTime.Today));
            if (t < f) (f, t) = (t, f);

            var results = await _db.KcEvents.AsNoTracking()
                .Where(e => e.ScanDate >= f && e.ScanDate <= t)
                .GroupBy(e => e.Result).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

            var incidents = await _db.KcIncidents.AsNoTracking()
                .Where(i => i.IncidentDate >= f && i.IncidentDate <= t)
                .Select(i => new { i.Type, i.Severity, i.PersonEnroll, i.PersonName, i.KeyName, i.KeyEnroll, i.Status })
                .ToListAsync();
            var serious = incidents.Where(i => i.Severity <= 2 && i.Status != KcStatus.Dismissed).ToList();

            var depts = (await _db.EmployeeMasters.AsNoTracking().ToListAsync())
                .GroupBy(e => e.EnrollNumber)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsActive).First().Department);

            var vm = new AuditViewModel
            {
                From = f,
                To = t,
                KeyScans = results.Sum(r => r.Count),
                ScansOk = results.Where(r => r.Key == KcResult.Ok).Sum(r => r.Count),
                ScansNoFace = results.Where(r => r.Key == KcResult.NoFace).Sum(r => r.Count),
                ScansNotAuthorized = results.Where(r => r.Key == KcResult.NotAuthorized).Sum(r => r.Count),
                ByType = incidents.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.Count()),
                People = serious.Where(i => i.PersonEnroll != null)
                    .GroupBy(i => i.PersonEnroll!.Value)
                    .Select(g => new AuditPersonRow
                    {
                        Enroll = g.Key,
                        Name = g.First().PersonName ?? $"#{g.Key}",
                        Department = depts.TryGetValue(g.Key, out var dep) ? dep : null,
                        Total = g.Count(),
                        NotAuthorized = g.Count(x => x.Type == KcIncidentType.NotAuthorized),
                        NoFace = g.Count(x => x.Type == KcIncidentType.NoFace),
                        TakenWithoutScan = g.Count(x => x.Type == KcIncidentType.TakenWithoutScan),
                        ReturnNotScanned = g.Count(x => x.Type == KcIncidentType.ReturnNotScanned)
                    })
                    .OrderByDescending(p => p.Total).Take(25).ToList(),
                Keys = serious.GroupBy(i => new { i.KeyEnroll, i.KeyName })
                    .Select(g => new AuditCountRow { Name = g.Key.KeyName, KeyEnroll = g.Key.KeyEnroll, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(20).ToList(),
                Departments = serious.Where(i => i.PersonEnroll != null)
                    .GroupBy(i => depts.TryGetValue(i.PersonEnroll!.Value, out var dep) && !string.IsNullOrWhiteSpace(dep) ? dep! : "Unknown")
                    .Select(g => new AuditCountRow { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(20).ToList(),
                Confirmed = incidents.Count(i => i.Status == KcStatus.Confirmed),
                Dismissed = incidents.Count(i => i.Status == KcStatus.Dismissed)
            };
            return View(vm);
        }

        /// <summary>Re-check a period, e.g. after correcting key authorizations.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recheck(DateOnly from, DateOnly to)
        {
            if (to < from) (from, to) = (to, from);
            var n = await _db.KcProcessedDays.Where(p => p.Day >= from && p.Day <= to).ExecuteDeleteAsync();
            TempData["Success"] = $"{n} day(s) queued for re-checking. Results update within a minute.";
            return RedirectToAction(nameof(Audit), new { from, to });
        }

        // =============================================
        // EMPLOYEE PHOTO (attendance software's Employee.photo, JPEG)
        // =============================================
        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Photo(int id)
        {
            if (id < 1 || id > 1000) return NotFound();
            try
            {
                var photo = await _db.Database.SqlQueryRaw<byte[]>(
                    @"SELECT TOP 1 CAST(photo AS varbinary(max)) AS Value
                      FROM dbo.Employee
                      WHERE TRY_CAST(emp_id AS int) = {0} AND DATALENGTH(photo) > 100
                      ORDER BY id DESC", id).ToListAsync();
                if (photo.Count == 0 || photo[0] == null) return NotFound();
                return File(photo[0], "image/jpeg");
            }
            catch
            {
                return NotFound();
            }
        }

        // =============================================
        // PRINTABLE REPORTS
        // =============================================

        /// <summary>Daily Security Report — one A4 page: totals, every problem, keys not returned, signatures.</summary>
        public async Task<IActionResult> DailyReport(DateOnly? date, bool print = false)
        {
            var d = await _q.ResolveDateAsync(date);
            var board = await _q.BuildBoardAsync(d);
            var events = await _db.KcEvents.AsNoTracking().Where(e => e.ScanDate == d)
                .Select(e => e.Result).ToListAsync();

            var vm = new DailyReportViewModel
            {
                Date = d,
                IsToday = board.IsToday,
                DayOver = await _db.KcProcessedDays.AnyAsync(p => p.Day == d && p.Settled),
                PrintedAt = DateTime.Now,
                PrintedBy = User.Identity?.Name ?? "",
                AutoPrint = print,
                KeysTaken = board.Keys.Count(k => k.State != KeyTileState.OnBoard),
                KeysReturned = board.Keys.Count(k => k.State == KeyTileState.Returned),
                KeysNotMoved = board.Keys.Count(k => k.State == KeyTileState.OnBoard),
                ScansTotal = events.Count,
                ScansOk = events.Count(r => r == KcResult.Ok),
                ScansWrongPerson = events.Count(r => r == KcResult.NotAuthorized),
                ScansNoFace = events.Count(r => r == KcResult.NoFace),
                Problems = await _db.KcIncidents.AsNoTracking()
                    .Where(i => i.IncidentDate == d && i.Severity <= 2)
                    .OrderBy(i => i.OccurredAt).ToListAsync(),
                StillOut = board.Keys
                    .Where(k => k.State is KeyTileState.Out or KeyTileState.OutProblem or KeyTileState.Overdue or KeyTileState.NotReturned)
                    .OrderBy(k => k.Since).ToList()
            };
            return View(vm);
        }

        /// <summary>Printable key list: list=out (keys still out — tick while collecting) or list=nottaken.</summary>
        public async Task<IActionResult> KeyList(DateOnly? date, string list = "out", bool print = false)
        {
            var d = await _q.ResolveDateAsync(date);
            var board = await _q.BuildBoardAsync(d);
            list = list == "nottaken" ? "nottaken" : "out";

            var keys = list == "out"
                ? board.Keys.Where(k => k.State is KeyTileState.Out or KeyTileState.OutProblem or KeyTileState.Overdue or KeyTileState.NotReturned)
                            .OrderBy(k => k.Since).ToList()
                : board.Keys.Where(k => k.State == KeyTileState.OnBoard).OrderBy(k => k.KeyName).ToList();

            // authorized names per key (active employees only)
            var keyIds = keys.Select(k => k.KeyEnroll).ToList();
            var auth = await _db.KeyAuthorizations.AsNoTracking().Where(a => keyIds.Contains(a.KeyEnroll))
                .Join(_db.EmployeeMasters.Where(e => e.IsActive), a => a.EmpEnroll, e => e.EnrollNumber,
                      (a, e) => new { a.KeyEnroll, e.FullName })
                .ToListAsync();

            return View(new KeyListReportViewModel
            {
                Date = d,
                IsToday = board.IsToday,
                List = list,
                PrintedAt = DateTime.Now,
                PrintedBy = User.Identity?.Name ?? "",
                AutoPrint = print,
                Keys = keys,
                AuthorizedNames = auth.GroupBy(a => a.KeyEnroll)
                    .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.FullName).Distinct().OrderBy(n => n)))
            });
        }

        // =============================================
        // MONITOR MODE (full screen for a TV / second screen at the security desk)
        // =============================================
        public IActionResult Monitor() => View();

        [HttpGet]
        public async Task<IActionResult> MonitorData()
        {
            var d = await _q.ResolveDateAsync(null);
            var board = await _q.BuildBoardAsync(d);
            var events = await _db.KcEvents.AsNoTracking().Where(e => e.ScanDate == d)
                .OrderByDescending(e => e.ScanTime).ThenByDescending(e => e.Id).ToListAsync();

            object Item(KcEvent e) => new
            {
                id = e.Id,
                time = e.ScanTime.ToString("HH:mm:ss"),
                key = e.KeyName.Replace("key_", "").Trim(),
                direction = e.Direction,
                result = e.Result,
                person = e.FaceName,
                personEnroll = e.FaceEnroll
            };

            var groups = board.Keys.GroupBy(KeyCustodyQueries.Group).ToDictionary(g => g.Key, g => g.Count());
            int G(string g) => groups.TryGetValue(g, out var n) ? n : 0;

            return Json(new
            {
                date = d.ToString("dddd, dd MMM yyyy"),
                isToday = d == DateOnly.FromDateTime(DateTime.Today),
                spot = events.Count > 0 ? Item(events[0]) : null,
                recent = events.Skip(1).Take(6).Select(Item),
                stats = new
                {
                    keysOut = G("out") + G("attention"),
                    attention = G("attention"),
                    returned = G("returned"),
                    onBoard = G("board"),
                    correct = events.Count(e => e.Result == KcResult.Ok),
                    problems = events.Count(e => e.Result != KcResult.Ok),
                    openIncidents = await _db.KcIncidents.CountAsync(i => i.IncidentDate == d && i.Severity <= 2 && i.Status == KcStatus.Open)
                }
            });
        }

        // =============================================
        // LIVE SCAN FEED (Dashboard right panel, polled every few seconds)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Feed(DateOnly? date, int take = 8)
        {
            var d = await _q.ResolveDateAsync(date);
            take = Math.Clamp(take, 1, 30);
            var items = await _db.KcEvents.AsNoTracking()
                .Where(e => e.ScanDate == d)
                .OrderByDescending(e => e.ScanTime).ThenByDescending(e => e.Id)
                .Take(take)
                .Select(e => new
                {
                    id = e.Id,
                    time = e.ScanTime,
                    key = e.KeyName,
                    direction = e.Direction,
                    result = e.Result,
                    person = e.FaceName,
                    personEnroll = e.FaceEnroll
                }).ToListAsync();

            return Json(items.Select(e => new
            {
                e.id,
                time = e.time.ToString("HH:mm:ss"),
                key = e.key.Replace("key_", "").Trim(),
                e.direction,
                e.result,
                e.person,
                e.personEnroll,
                url = Url.Action(nameof(Movement), new { id = e.id })
            }));
        }

        // =============================================
        // LIVE ALERTS (polled by every KMS page)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Alerts(int sinceId = 0)
        {
            var maxId = await _db.KcIncidents.AnyAsync() ? await _db.KcIncidents.MaxAsync(i => i.Id) : 0;
            if (sinceId <= 0) return Json(new { maxId, items = Array.Empty<object>() });

            var recent = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
            var items = await _db.KcIncidents.AsNoTracking()
                .Where(i => i.Id > sinceId && i.Severity <= 2 && i.IncidentDate >= recent && i.Status == KcStatus.Open)
                .OrderBy(i => i.Id).Take(10)
                .Select(i => new
                {
                    id = i.Id,
                    type = i.Type,
                    key = i.KeyName,
                    person = i.PersonName,
                    personEnroll = i.PersonEnroll,
                    time = i.OccurredAt,
                    summary = i.Summary,
                    severity = i.Severity
                }).ToListAsync();
            return Json(new { maxId, items });
        }
    }
}
