using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Services;
using IsgCevreYonetim.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Controllers
{
    [Authorize]
    public sealed class OnlineUsersController : Controller
    {
        private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IHubContext<OnlineUsersHub> _onlineUsersHub;

        public OnlineUsersController(ApplicationDbContext context, IUserScopeService userScopeService, IHubContext<OnlineUsersHub> onlineUsersHub)
        {
            _context = context;
            _userScopeService = userScopeService;
            _onlineUsersHub = onlineUsersHub;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var isAjax = string.Equals(Request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            // Kaydı sayfayı açan kullanıcının da ilk yüklemede çevrimiçi görünmesi için oluştur.
            if (!isAjax)
                await TouchPresenceAsync(scope);

            var now = DateTime.UtcNow;
            var cutoff = now.Subtract(OnlineWindow);
            // Eski heartbeat'leri temizleyerek tabloyu küçük tut.
            if (!isAjax)
            {
                await _context.OnlineKullaniciOturumlari
                    .Where(x => x.LastSeenUtc < now.AddDays(-1))
                    .ExecuteDeleteAsync();
            }

            var activeSessions = await _context.OnlineKullaniciOturumlari
                .AsNoTracking()
                .Where(x => x.CompanyId == scope.ActiveCompanyId &&
                            x.BranchId == scope.ActiveBranchId &&
                            x.LastSeenUtc >= cutoff)
                .GroupBy(x => x.PersonelId)
                .Select(group => new
                {
                    PersonelId = group.Key,
                    LastSeenUtc = group.Max(x => x.LastSeenUtc)
                })
                .ToListAsync();

            var personelIds = activeSessions.Select(x => x.PersonelId).ToArray();
            var people = personelIds.Length == 0
                ? new List<OnlineUserRow>()
                : await _context.Personeller
                    .AsNoTracking()
                    .Where(x => personelIds.Contains(x.Id) && x.AktifMi)
                    .Select(x => new OnlineUserRow
                    {
                        PersonelId = x.Id,
                        AdSoyad = x.Ad + " " + x.Soyad,
                        SicilNo = x.SicilNo,
                        Departman = x.Department != null ? x.Department.DepartmentAdi : "—",
                        Gorev = x.Gorev != null ? x.Gorev.Ad : "—"
                    })
                    .ToListAsync();

            var lastSeenByPersonel = activeSessions.ToDictionary(x => x.PersonelId, x => x.LastSeenUtc);
            foreach (var person in people)
                person.LastSeenUtc = lastSeenByPersonel[person.PersonelId];

            var model = people.OrderBy(x => x.AdSoyad, StringComparer.CurrentCultureIgnoreCase).ToList();
            ViewBag.BranchName = scope.ActiveBranchName;
            ViewBag.OnlineCount = model.Count;

            if (isAjax)
                return PartialView("_OnlineRows", model);

            return View(model);
        }

        // Kendi heartbeat'ini tutan uçtur; başka kullanıcı bilgisi döndürmez.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ping()
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Unauthorized();

            await TouchPresenceAsync(scope);
            return NoContent();
        }

        private async Task TouchPresenceAsync(UserScopeInfo scope)
        {
            var sessionKey = User.FindFirstValue("OnlineSessionKey");
            if (string.IsNullOrWhiteSpace(sessionKey))
                sessionKey = $"legacy-{scope.PersonelId}";

            var now = DateTime.UtcNow;
            var existing = await _context.OnlineKullaniciOturumlari
                .AsNoTracking()
                .Where(x => x.SessionKey == sessionKey)
                .Select(x => new { x.CompanyId, x.BranchId })
                .SingleOrDefaultAsync();
            var presenceChanged = existing == null ||
                                  existing.CompanyId != scope.ActiveCompanyId ||
                                  existing.BranchId != scope.ActiveBranchId;

            // MERGE + HOLDLOCK aynı oturumdan birden fazla sekme ilk heartbeat'i aynı anda
            // yolladığında da tek satır kalmasını sağlar.
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
MERGE [dbo].[OnlineKullaniciOturumlari] WITH (HOLDLOCK) AS target
USING (SELECT CAST({sessionKey} AS nvarchar(64)) AS [SessionKey]) AS source
ON target.[SessionKey] = source.[SessionKey]
WHEN MATCHED THEN UPDATE SET
    [PersonelId] = {scope.PersonelId},
    [CompanyId] = {scope.ActiveCompanyId},
    [BranchId] = {scope.ActiveBranchId},
    [LastSeenUtc] = {now}
WHEN NOT MATCHED THEN INSERT ([SessionKey], [PersonelId], [CompanyId], [BranchId], [LastSeenUtc])
    VALUES (source.[SessionKey], {scope.PersonelId}, {scope.ActiveCompanyId}, {scope.ActiveBranchId}, {now});");

            if (presenceChanged)
                await _onlineUsersHub.Clients.All.SendAsync("OnlineUsersChanged");
        }
    }

    public sealed class OnlineUserRow
    {
        public int PersonelId { get; set; }
        public string AdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public DateTime LastSeenUtc { get; set; }
    }
}
