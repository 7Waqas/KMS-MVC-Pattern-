using kms.Models;
using kms.Models.KeyCustody;
using kms.Models.ViewModels;
using kms.Services.KeyCustody;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace kms.Controllers
{
    /// <summary>Dashboard: one day at a glance. Built from the same data as the Key Board,
    /// so every number here matches every other screen, and every number is clickable.</summary>
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly KeyCustodyQueries _q;
        private readonly KeyCustodyOptions _o;

        public HomeController(ApplicationDbContext db, KeyCustodyQueries q, IOptions<KeyCustodyOptions> o)
        {
            _db = db;
            _q = q;
            _o = o.Value;
        }

        public async Task<IActionResult> Index(DateOnly? date)
        {
            var d = await _q.ResolveDateAsync(date);
            var events = await _db.KcEvents.AsNoTracking().Where(e => e.ScanDate == d).ToListAsync();
            var board = await _q.BuildBoardAsync(d);

            var vm = new HomeDashboardViewModel
            {
                Board = board,
                TotalEmployees = await _db.EmployeeMasters.CountAsync(e => e.IsActive),
                ScansTotal = events.Count,
                ScansOk = events.Count(e => e.Result == KcResult.Ok),
                ScansNoFace = events.Count(e => e.Result == KcResult.NoFace),
                ScansWrongPerson = events.Count(e => e.Result == KcResult.NotAuthorized),
                IncidentsOpen = await _db.KcIncidents.CountAsync(i => i.IncidentDate == d && i.Severity <= 2 && i.Status == KcStatus.Open),
                Latest = events.OrderByDescending(e => e.ScanTime).ThenByDescending(e => e.Id).Take(9).ToList(),
                ClosingHour = _o.OverdueHour
            };

            // chart: bars = keys taken / returned per hour, red line = problem scans
            foreach (var e in events)
            {
                var h = e.ScanTime.Hour;
                if (e.Direction == KcDirection.Out) vm.HourOut[h]++; else vm.HourIn[h]++;
                if (e.Result != KcResult.Ok) vm.HourProblem[h]++;
            }

            return View(vm);
        }
    }
}
