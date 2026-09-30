using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Hubs;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class BildirimController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<BildirimHub> _hub;
    private readonly IBildirimService _bildirimService;
    public BildirimController(ApplicationDbContext db, IHubContext<BildirimHub> hub, IBildirimService bildirimService) { _db = db; _hub = hub; _bildirimService = bildirimService; }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var personelId = PersonelId();
        if (!personelId.HasValue) return Unauthorized();
        await _bildirimService.GecersizTehlikeliIsBildirimleriniTemizleAsync(personelId.Value);
        var items = await _db.Bildirimler.AsNoTracking().Where(x => x.PersonelId == personelId && x.Tur != "mesaj-ozel" && x.Tur != "mesaj-grup")
            .OrderBy(x => x.OkunduMu).ThenByDescending(x => x.CreatedDate).Take(100).ToListAsync();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Liste()
    {
        var personelId = PersonelId();
        if (!personelId.HasValue) return Unauthorized();
        await _bildirimService.GecersizTehlikeliIsBildirimleriniTemizleAsync(personelId.Value);
        var unread = await _db.Bildirimler.AsNoTracking().CountAsync(x => x.PersonelId == personelId && !x.OkunduMu && x.Tur != "mesaj-ozel" && x.Tur != "mesaj-grup");
        var items = await _db.Bildirimler.AsNoTracking().Where(x => x.PersonelId == personelId && x.Tur != "mesaj-ozel" && x.Tur != "mesaj-grup")
            .OrderBy(x => x.OkunduMu).ThenByDescending(x => x.CreatedDate).Take(30)
            .Select(x => new { x.Id, x.Baslik, x.Mesaj, x.Url, x.Tur, x.OkunduMu, x.CreatedDate }).ToListAsync();
        return Json(new { unread, items });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TumunuOkundu()
    {
        var personelId = PersonelId();
        if (!personelId.HasValue) return Unauthorized();
        var now = DateTime.Now;
        await _db.Bildirimler.Where(x => x.PersonelId == personelId && !x.OkunduMu && x.Tur != "mesaj-ozel" && x.Tur != "mesaj-grup")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.OkunduMu, true).SetProperty(x => x.OkunmaTarihi, now).SetProperty(x => x.UpdatedDate, now));
        await _hub.Clients.Group(BildirimHub.Grup(personelId.Value)).SendAsync("BildirimlerDegisti");
        return Json(new { success = true });
    }

    private int? PersonelId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
