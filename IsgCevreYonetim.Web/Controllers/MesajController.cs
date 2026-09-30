using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Hubs;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public sealed class MesajController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUserScopeService _scope;
    private readonly IHubContext<MesajHub> _hub;
    private readonly ILogger<MesajController> _logger;
    private readonly MessagePushQueue _pushQueue;
    private readonly MessageAttachmentStorage _attachments;
    private bool IsAsyncGroupAction => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private IActionResult GroupActionResult(string message, bool success, int? grupId = null)
    {
        if (IsAsyncGroupAction)
            return success ? Json(new { message }) : BadRequest(new { message });
        TempData[success ? "ToastrSuccess" : "ToastrError"] = message;
        return RedirectToAction(nameof(Index), success ? null : new { grupId });
    }

    public MesajController(ApplicationDbContext db, IUserScopeService scope, IHubContext<MesajHub> hub,
        ILogger<MesajController> logger, MessagePushQueue pushQueue, MessageAttachmentStorage attachments)
    {
        _db = db;
        _scope = scope;
        _hub = hub;
        _logger = logger;
        _pushQueue = pushQueue;
        _attachments = attachments;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? kisiId, int? grupId)
    {
        var timer = Stopwatch.StartNew();
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var scopeMs = timer.Elapsed.TotalMilliseconds;
        var people = await LoadPeopleAsync(scope);
        var peopleMs = timer.Elapsed.TotalMilliseconds - scopeMs;
        ViewBag.SeciliKisiId = kisiId;
        var groups = await LoadGroupsAsync(scope);
        var groupsMs = timer.Elapsed.TotalMilliseconds - scopeMs - peopleMs;
        static string Ms(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
        Response.Headers["Server-Timing"] =
            $"scope;dur={Ms(scopeMs)}, people;dur={Ms(peopleMs)}, groups;dur={Ms(groupsMs)}";
        if (grupId.HasValue && !groups.Any(x => x.Id == grupId)) grupId = null;
        return View(new MesajSayfaModel { Kisiler = people, SeciliKisiId = kisiId,
            AktifSubeId = scope.ActiveBranchId, BenimId = scope.PersonelId, Gruplar = groups, SeciliGrupId = grupId });
    }

    [HttpGet]
    public async Task<IActionResult> KisiListesi(int? kisiId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var people = await LoadPeopleAsync(scope);
        ViewBag.SeciliKisiId = kisiId;
        return PartialView("_KisiListesi", people);
    }

    [HttpGet]
    public async Task<IActionResult> CevrimiciKisiler()
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Unauthorized();
        var since = DateTime.UtcNow.AddMinutes(-2);
        var ids = await _db.OnlineKullaniciOturumlari.AsNoTracking()
            .Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
                x.LastSeenUtc >= since && x.PersonelId != scope.PersonelId)
            .Select(x => x.PersonelId).Distinct().ToListAsync();
        return Json(ids);
    }

    private async Task<List<MesajKisi>> LoadPeopleAsync(UserScopeInfo scope)
    {
        var people = await _db.Personeller.AsNoTracking()
            .Where(x => x.Id != scope.PersonelId && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId && x.AktifMi)
            .OrderBy(x => x.Ad).ThenBy(x => x.Soyad)
            .Select(x => new MesajKisi { Id = x.Id, AdSoyad = x.Ad + " " + x.Soyad,
                SicilNo = x.SicilNo, BranchId = x.BranchId })
            .ToListAsync();
        var onlineIds = await _db.OnlineKullaniciOturumlari.AsNoTracking()
            .Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId && x.LastSeenUtc >= DateTime.UtcNow.AddMinutes(-2))
            .Select(x => x.PersonelId).Distinct().ToListAsync();
        var online = onlineIds.ToHashSet();
        var unread = await _db.OzelMesajlar.AsNoTracking()
            .Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId && x.AliciPersonelId == scope.PersonelId && x.OkunmaUtc == null)
            .GroupBy(x => x.GonderenPersonelId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var recent = await _db.OzelMesajlar.AsNoTracking()
            .Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
                (x.GonderenPersonelId == scope.PersonelId || x.AliciPersonelId == scope.PersonelId))
            .GroupBy(x => x.GonderenPersonelId == scope.PersonelId ? x.AliciPersonelId : x.GonderenPersonelId)
            .Select(g => g.OrderByDescending(x => x.Id)
                .Select(x => new OzelMesaj
                {
                    Id = x.Id, GonderenPersonelId = x.GonderenPersonelId,
                    AliciPersonelId = x.AliciPersonelId, Icerik = x.Icerik, GonderimUtc = x.GonderimUtc
                }).First()).ToListAsync();
        var lastByPerson = recent.ToDictionary(
            x => x.GonderenPersonelId == scope.PersonelId ? x.AliciPersonelId : x.GonderenPersonelId);
        foreach (var person in people)
        {
            person.Okunmamis = unread.GetValueOrDefault(person.Id);
            person.Cevrimici = online.Contains(person.Id);
            if (lastByPerson.TryGetValue(person.Id, out var last))
            {
                person.SonMesaj = string.IsNullOrWhiteSpace(last.Icerik) ? "📎 Dosya" : last.Icerik;
                person.SonMesajUtc = last.GonderimUtc;
                person.SonMesajBenden = last.GonderenPersonelId == scope.PersonelId;
            }
        }
        return people.OrderByDescending(x => x.Okunmamis > 0)
            .ThenByDescending(x => x.SonMesajUtc).ThenBy(x => x.AdSoyad).ToList();
    }

    private async Task<List<MesajGrupSatiri>> LoadGroupsAsync(UserScopeInfo scope)
    {
        var accepted = await (from member in _db.Set<MesajGrubuUyelik>().AsNoTracking()
            join grp in _db.Set<MesajGrubu>().AsNoTracking() on member.GrupId equals grp.Id
            where member.PersonelId == scope.PersonelId && member.KabulUtc != null && member.RedUtc == null &&
                  grp.CompanyId == scope.ActiveCompanyId && grp.BranchId == scope.ActiveBranchId
            select new MesajGrupSatiri { Id = grp.Id, Ad = grp.Ad, KurucuMu = grp.KurucuPersonelId == scope.PersonelId })
            .ToListAsync();
        var ids = accepted.Select(x => x.Id).ToArray();
        if (ids.Length != 0)
        {
            var last = await _db.Set<MesajGrubu>().AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, Last = _db.Set<MesajGrubuMesaji>()
                    .Where(m => m.GrupId == x.Id).OrderByDescending(m => m.Id)
                    .Select(m => (DateTime?)m.GonderimUtc).FirstOrDefault() }).ToListAsync();
            var recent = last.ToDictionary(x => x.Id, x => x.Last);
            foreach (var row in accepted) if (recent.TryGetValue(row.Id, out var date)) row.SonMesajUtc = date;
        }
        return accepted.OrderByDescending(x => x.SonMesajUtc).ThenBy(x => x.Ad).ToList();
    }

    [HttpGet]
    public async Task<IActionResult> GrupKonusma(int grupId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var member = await AcceptedMemberAsync(grupId, scope);
        if (member == null) return NotFound();
        await _db.Bildirimler.Where(x => x.PersonelId == scope.PersonelId && x.Tur == "mesaj-grup" &&
            x.Url == "/Mesaj?grupId=" + grupId && !x.OkunduMu)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.OkunduMu, true).SetProperty(x => x.OkunmaTarihi, DateTime.Now));
        var messages = await (from msg in _db.Set<MesajGrubuMesaji>().AsNoTracking()
            join person in _db.Personeller.AsNoTracking() on msg.GonderenPersonelId equals person.Id
            where msg.GrupId == grupId && msg.GonderimUtc >= member.KabulUtc!.Value
            orderby msg.Id descending
            select new { msg.Id, msg.GonderenPersonelId, msg.Icerik, msg.GonderimUtc, Gonderen = person.Ad + " " + person.Soyad })
            .Take(100).ToListAsync();
        var attachments = await LoadAttachmentsAsync(messages.Select(x => x.Id), group: true);
        return Json(messages.OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.GonderenPersonelId, x.Icerik, x.GonderimUtc, x.Gonderen,
            Ekler = attachments.GetValueOrDefault(x.Id, Array.Empty<MessageAttachmentItem>())
        }));
    }

    // Yalnızca mesajı yazan kişi veya grup kurucusu grup mesajını silebilir.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrupMesajSil(int grupId, long mesajId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        if (await AcceptedMemberAsync(grupId, scope) == null) return NotFound();
        var founder = await _db.Set<MesajGrubu>().AsNoTracking()
            .AnyAsync(x => x.Id == grupId && x.CompanyId == scope.ActiveCompanyId &&
                x.BranchId == scope.ActiveBranchId && x.KurucuPersonelId == scope.PersonelId);
        var files = await _db.MesajEkleri.AsNoTracking()
            .Where(x => x.GrupMesajiId == mesajId && x.GrupMesaji != null && x.GrupMesaji.GrupId == grupId &&
                (founder || x.GrupMesaji.GonderenPersonelId == scope.PersonelId))
            .Select(x => x.DepoAdi).ToListAsync();
        var deleted = await _db.Set<MesajGrubuMesaji>()
            .Where(x => x.Id == mesajId && x.GrupId == grupId && (founder || x.GonderenPersonelId == scope.PersonelId))
            .ExecuteDeleteAsync();
        if (deleted == 0) return NotFound();
        DeleteFileNames(files);
        var members = await _db.Set<MesajGrubuUyelik>().AsNoTracking()
            .Where(x => x.GrupId == grupId && x.KabulUtc != null && x.RedUtc == null)
            .Select(x => x.PersonelId).ToListAsync();
        if (members.Count > 0)
            await _hub.Clients.Groups(members.Select(MesajHub.Grup).ToList())
                .SendAsync("GrupMesajiDegisti", grupId);
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrupSil(int grupId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var chatGroup = await _db.Set<MesajGrubu>().SingleOrDefaultAsync(x => x.Id == grupId &&
            x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
            x.KurucuPersonelId == scope.PersonelId);
        if (chatGroup == null)
        {
            return GroupActionResult("Grup bulunamadı veya grubu silme yetkiniz yok.", false);
        }
        var members = await _db.Set<MesajGrubuUyelik>().AsNoTracking()
            .Where(x => x.GrupId == grupId).Select(x => x.PersonelId).ToListAsync();
        var files = await _db.MesajEkleri.AsNoTracking()
            .Where(x => x.GrupMesaji != null && x.GrupMesaji.GrupId == grupId)
            .Select(x => x.DepoAdi).ToListAsync();
        // Canlı veritabanındaki FK silme davranışından bağımsız, tek işlemde sil.
        try
        {
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                await _db.Bildirimler.Where(x => x.Tur == "mesaj-grup" && x.Url == "/Mesaj?grupId=" + grupId)
                    .ExecuteDeleteAsync();
                await _db.Set<MesajGrubuMesaji>().Where(x => x.GrupId == grupId).ExecuteDeleteAsync();
                await _db.Set<MesajGrubuUyelik>().Where(x => x.GrupId == grupId).ExecuteDeleteAsync();
                await _db.Set<MesajGrubu>().Where(x => x.Id == grupId &&
                    x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
                    x.KurucuPersonelId == scope.PersonelId).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj grubu silinemedi. GrupId: {GrupId}", grupId);
            return GroupActionResult("Grup silinemedi. Sunucu kayıtlarını kontrol edin.", false, grupId);
        }
        DeleteFileNames(files);
        if (members.Count > 0)
        {
            try
            {
                await _hub.Clients.Groups(members.Select(MesajHub.Grup).ToList())
                    .SendAsync("GrupSilindi", grupId);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Grup silme bildirimi gönderilemedi. GrupId: {GrupId}", grupId); }
        }
        return GroupActionResult("Grup silindi.", true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GruptanAyril(int grupId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var membership = await (from m in _db.Set<MesajGrubuUyelik>()
            join g in _db.Set<MesajGrubu>() on m.GrupId equals g.Id
            where m.GrupId == grupId && m.PersonelId == scope.PersonelId && m.KabulUtc != null &&
                m.RedUtc == null && g.CompanyId == scope.ActiveCompanyId && g.BranchId == scope.ActiveBranchId
            select new { Member = m, Founder = g.KurucuPersonelId == scope.PersonelId }).SingleOrDefaultAsync();
        if (membership == null)
        {
            return GroupActionResult("Grup üyeliği bulunamadı.", false);
        }
        if (membership.Founder)
            return GroupActionResult("Grup kurucusu gruptan ayrılamaz; grubu silebilir.", false, grupId);
        try
        {
            _db.Set<MesajGrubuUyelik>().Remove(membership.Member);
            await _db.SaveChangesAsync();
            await _db.Bildirimler.Where(x => x.PersonelId == scope.PersonelId && x.Tur == "mesaj-grup" &&
                x.Url == "/Mesaj?grupId=" + grupId).ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gruptan ayrılamadı. GrupId: {GrupId}", grupId);
            return GroupActionResult("Gruptan ayrılamadınız. Sunucu kayıtlarını kontrol edin.", false, grupId);
        }
        return GroupActionResult("Gruptan ayrıldınız.", true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrupKur(string? ad, int[]? kisiIds)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        ad = ad?.Trim();
        var ids = kisiIds?.Distinct().Where(x => x != scope.PersonelId).ToArray() ?? Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(ad) || ad.Length > 120 || ids.Length == 0 || ids.Length > 100)
            return BadRequest("Grup adı ve en az bir davetli seçin.");
        var valid = await _db.Personeller.CountAsync(x => ids.Contains(x.Id) && x.AktifMi && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        if (valid != ids.Length) return BadRequest("Davetlilerden biri uygun değil.");
        var now = DateTime.UtcNow;
        var chatGroup = new MesajGrubu { Ad = ad, CompanyId = scope.ActiveCompanyId, BranchId = scope.ActiveBranchId,
            KurucuPersonelId = scope.PersonelId, OlusturmaUtc = now };
        _db.Set<MesajGrubu>().Add(chatGroup);
        await _db.SaveChangesAsync();
        _db.Set<MesajGrubuUyelik>().Add(new MesajGrubuUyelik { GrupId = chatGroup.Id, PersonelId = scope.PersonelId,
            DavetEdenPersonelId = scope.PersonelId, DavetUtc = now, KabulUtc = now });
        foreach (var id in ids) _db.Set<MesajGrubuUyelik>().Add(new MesajGrubuUyelik { GrupId = chatGroup.Id,
            PersonelId = id, DavetEdenPersonelId = scope.PersonelId, DavetUtc = now });
        await _db.SaveChangesAsync();
        await _hub.Clients.Groups(ids.Select(MesajHub.Grup).ToList()).SendAsync("GrupDaveti", chatGroup.Ad);
        return Json(new { chatGroup.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Davetler()
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var invitations = await (from member in _db.Set<MesajGrubuUyelik>().AsNoTracking()
            join grp in _db.Set<MesajGrubu>().AsNoTracking() on member.GrupId equals grp.Id
            join sender in _db.Personeller.AsNoTracking() on member.DavetEdenPersonelId equals sender.Id
            where member.PersonelId == scope.PersonelId && member.KabulUtc == null && member.RedUtc == null &&
                grp.CompanyId == scope.ActiveCompanyId && grp.BranchId == scope.ActiveBranchId
            orderby member.DavetUtc descending
            select new { member.Id, GrupAdi = grp.Ad, DavetEden = sender.Ad + " " + sender.Soyad }).ToListAsync();
        return Json(invitations);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DavetiYanitla(int davetId, bool kabul)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var member = await (from m in _db.Set<MesajGrubuUyelik>()
            join g in _db.Set<MesajGrubu>() on m.GrupId equals g.Id
            where m.Id == davetId && m.PersonelId == scope.PersonelId && m.KabulUtc == null && m.RedUtc == null &&
                g.CompanyId == scope.ActiveCompanyId && g.BranchId == scope.ActiveBranchId
            select m).SingleOrDefaultAsync();
        if (member == null) return NotFound();
        if (kabul) member.KabulUtc = DateTime.UtcNow; else member.RedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(MesajHub.Grup(member.DavetEdenPersonelId)).SendAsync("GrupUyelikDegisti");
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MessageAttachmentStorage.MaxRequest)]
    [RequestFormLimits(MultipartBodyLengthLimit = MessageAttachmentStorage.MaxRequest)]
    public async Task<IActionResult> GrupGonder(int grupId, string? icerik, List<IFormFile>? dosyalar)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        icerik = icerik?.Trim();
        dosyalar ??= new();
        if ((string.IsNullOrWhiteSpace(icerik) && dosyalar.Count == 0) || (icerik?.Length ?? 0) > 2000)
            return BadRequest("Mesaj veya dosya gönderin (metin en fazla 2000 karakter). ");
        if (MessageAttachmentStorage.Validate(dosyalar) is { } groupError) return BadRequest(groupError);
        var member = await AcceptedMemberAsync(grupId, scope);
        if (member == null) return NotFound();
        var recipients = await _db.Set<MesajGrubuUyelik>().AsNoTracking()
            .Where(x => x.GrupId == grupId && x.KabulUtc != null && x.RedUtc == null &&
                x.PersonelId != scope.PersonelId)
            .Select(x => x.PersonelId).ToListAsync();
        var message = new MesajGrubuMesaji { GrupId = grupId, GonderenPersonelId = scope.PersonelId,
            Icerik = icerik ?? string.Empty, GonderimUtc = DateTime.UtcNow };
        _db.Set<MesajGrubuMesaji>().Add(message);
        var saved = await SaveAttachmentsAsync(dosyalar);
        if (saved.Error != null) return BadRequest(saved.Error);
        foreach (var attachment in saved.Items) { attachment.GrupMesaji = message; _db.MesajEkleri.Add(attachment); }
        var groupUrl = $"/Mesaj?grupId={grupId}";
        foreach (var id in recipients)
            _db.Bildirimler.Add(MessageNotification(id, "Yeni grup mesajı", groupUrl, "mesaj-grup"));
        try { await _db.SaveChangesAsync(); }
        catch { DeleteFiles(saved.Items); throw; }
        if (recipients.Count > 0)
        {
            await _hub.Clients.Groups(recipients.Select(MesajHub.Grup).ToList())
                .SendAsync("GrupMesajiDegisti", grupId, true);
        }
        foreach (var id in recipients)
            _pushQueue.Enqueue(new MessagePushItem(id, "Yeni grup mesajı", groupUrl, $"grup:{grupId}:{Guid.NewGuid():N}"));
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrubaDavetEt(int grupId, int[]? kisiIds)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var chatGroup = await _db.Set<MesajGrubu>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == grupId && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId && x.KurucuPersonelId == scope.PersonelId);
        if (chatGroup == null) return NotFound();
        var ids = kisiIds?.Distinct().Where(x => x != scope.PersonelId).ToArray() ?? Array.Empty<int>();
        if (ids.Length == 0 || ids.Length > 100) return BadRequest();
        var valid = await _db.Personeller.CountAsync(x => ids.Contains(x.Id) && x.AktifMi && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        if (valid != ids.Length) return BadRequest();
        var existing = await _db.Set<MesajGrubuUyelik>().Where(x => x.GrupId == grupId && ids.Contains(x.PersonelId)).Select(x => x.PersonelId).ToListAsync();
        var add = ids.Except(existing).ToArray();
        foreach (var id in add) _db.Set<MesajGrubuUyelik>().Add(new MesajGrubuUyelik { GrupId = grupId,
            PersonelId = id, DavetEdenPersonelId = scope.PersonelId, DavetUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        if (add.Length > 0)
            await _hub.Clients.Groups(add.Select(MesajHub.Grup).ToList())
                .SendAsync("GrupDaveti", chatGroup.Ad);
        return Json(new { davetSayisi = add.Length });
    }

    private async Task<MesajGrubuUyelik?> AcceptedMemberAsync(int grupId, UserScopeInfo scope) =>
        await (from m in _db.Set<MesajGrubuUyelik>().AsNoTracking()
            join g in _db.Set<MesajGrubu>().AsNoTracking() on m.GrupId equals g.Id
            where m.GrupId == grupId && m.PersonelId == scope.PersonelId && m.KabulUtc != null && m.RedUtc == null &&
                g.CompanyId == scope.ActiveCompanyId && g.BranchId == scope.ActiveBranchId
            select m).SingleOrDefaultAsync();

    [HttpGet]
    public async Task<IActionResult> Konusma(int kisiId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        if (!await AllowedAsync(kisiId, scope.ActiveCompanyId, scope.ActiveBranchId, scope.PersonelId)) return NotFound();
        var messages = await _db.OzelMesajlar.AsNoTracking()
            .Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
                ((x.GonderenPersonelId == scope.PersonelId && x.AliciPersonelId == kisiId) ||
                 (x.GonderenPersonelId == kisiId && x.AliciPersonelId == scope.PersonelId)))
            .OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { x.Id, x.GonderenPersonelId, x.Icerik, x.GonderimUtc, x.OkunmaUtc })
            .ToListAsync();
        var attachments = await LoadAttachmentsAsync(messages.Select(x => x.Id), group: false);
        return Json(messages.OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.GonderenPersonelId, x.Icerik, x.GonderimUtc, x.OkunmaUtc,
            Ekler = attachments.GetValueOrDefault(x.Id, Array.Empty<MessageAttachmentItem>())
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Okunmamis()
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Unauthorized();
        var count = await _db.OzelMesajlar.CountAsync(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
            x.AliciPersonelId == scope.PersonelId && x.OkunmaUtc == null);
        count += await _db.Bildirimler.CountAsync(x => x.PersonelId == scope.PersonelId && x.Tur == "mesaj-grup" && !x.OkunduMu);
        return Json(new { count });
    }

    // Özel mesaj yalnızca gönderen tarafından, aynı şirket ve şube kapsamında silinir.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MesajSil(int kisiId, long mesajId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        if (!await AllowedAsync(kisiId, scope.ActiveCompanyId, scope.ActiveBranchId, scope.PersonelId)) return NotFound();
        var files = await _db.MesajEkleri.AsNoTracking()
            .Where(x => x.OzelMesajId == mesajId && x.OzelMesaj != null &&
                x.OzelMesaj.CompanyId == scope.ActiveCompanyId && x.OzelMesaj.BranchId == scope.ActiveBranchId &&
                x.OzelMesaj.GonderenPersonelId == scope.PersonelId && x.OzelMesaj.AliciPersonelId == kisiId)
            .Select(x => x.DepoAdi).ToListAsync();
        var deleted = await _db.OzelMesajlar.Where(x => x.Id == mesajId &&
            x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
            x.GonderenPersonelId == scope.PersonelId && x.AliciPersonelId == kisiId)
            .ExecuteDeleteAsync();
        if (deleted == 0) return NotFound();
        DeleteFileNames(files);
        await _hub.Clients.Group(MesajHub.Grup(kisiId)).SendAsync("MesajDegisti", scope.PersonelId);
        await _hub.Clients.Group(MesajHub.Grup(scope.PersonelId)).SendAsync("MesajDegisti", kisiId);
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Yaziyor(int kisiId, bool yaziyor)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        if (!await AllowedAsync(kisiId, scope.ActiveCompanyId, scope.ActiveBranchId, scope.PersonelId))
            return NotFound();
        await _hub.Clients.Group(MesajHub.Grup(kisiId))
            .SendAsync("YazmaDurumu", scope.PersonelId, yaziyor);
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MessageAttachmentStorage.MaxRequest)]
    [RequestFormLimits(MultipartBodyLengthLimit = MessageAttachmentStorage.MaxRequest)]
    public async Task<IActionResult> Gonder(int kisiId, string? icerik, List<IFormFile>? dosyalar)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        icerik = icerik?.Trim();
        dosyalar ??= new();
        if ((string.IsNullOrWhiteSpace(icerik) && dosyalar.Count == 0) || (icerik?.Length ?? 0) > 2000)
            return BadRequest("Mesaj veya dosya gönderin (metin en fazla 2000 karakter). ");
        if (MessageAttachmentStorage.Validate(dosyalar) is { } fileError) return BadRequest(fileError);
        if (!await AllowedAsync(kisiId, scope.ActiveCompanyId, scope.ActiveBranchId, scope.PersonelId)) return NotFound();
        var message = new OzelMesaj { CompanyId = scope.ActiveCompanyId, BranchId = scope.ActiveBranchId,
            GonderenPersonelId = scope.PersonelId, AliciPersonelId = kisiId,
            Icerik = icerik ?? string.Empty, GonderimUtc = DateTime.UtcNow };
        _db.OzelMesajlar.Add(message);
        var saved = await SaveAttachmentsAsync(dosyalar);
        if (saved.Error != null) return BadRequest(saved.Error);
        foreach (var attachment in saved.Items) { attachment.OzelMesaj = message; _db.MesajEkleri.Add(attachment); }
        var sender = await _db.Personeller.AsNoTracking()
            .Where(x => x.Id == scope.PersonelId)
            .Select(x => x.Ad + " " + x.Soyad).FirstOrDefaultAsync();
        var title = (sender ?? "Bir personel") + " size mesaj gönderdi";
        var messageUrl = $"/Mesaj?kisiId={scope.PersonelId}";
        try { await _db.SaveChangesAsync(); }
        catch { DeleteFiles(saved.Items); throw; }
        await _hub.Clients.Group(MesajHub.Grup(kisiId)).SendAsync("MesajDegisti", scope.PersonelId);
        await _hub.Clients.Group(MesajHub.Grup(kisiId)).SendAsync("YeniMesaj",
            new { KisiId = scope.PersonelId, Gonderen = sender ?? "Bir personel", Icerik = icerik ?? "Dosya gönderdi" });
        await _hub.Clients.Group(MesajHub.Grup(scope.PersonelId)).SendAsync("MesajDegisti", kisiId);
        _pushQueue.Enqueue(new MessagePushItem(kisiId, title, messageUrl, $"mesaj:{message.Id}"));
        return Json(new { message.Id });
    }

    private static Bildirim MessageNotification(int personelId, string title, string url, string type) => new()
    {
        PersonelId = personelId, Baslik = title, Mesaj = "Yeni mesajınız var",
        Url = url, Tur = type, ReferansAnahtari = $"mesaj:{Guid.NewGuid():N}",
        CreatedDate = DateTime.Now, UpdatedDate = DateTime.Now, IsActive = true
    };

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Okundu(int kisiId)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        if (!await AllowedAsync(kisiId, scope.ActiveCompanyId, scope.ActiveBranchId, scope.PersonelId)) return NotFound();
        var updated = await _db.OzelMesajlar.Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
            x.GonderenPersonelId == kisiId && x.AliciPersonelId == scope.PersonelId && x.OkunmaUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.OkunmaUtc, DateTime.UtcNow));
        if (updated > 0)
        {
            await _hub.Clients.Group(MesajHub.Grup(kisiId)).SendAsync("MesajDegisti", scope.PersonelId);
            await _hub.Clients.Group(MesajHub.Grup(scope.PersonelId)).SendAsync("MesajDegisti", kisiId);
        }
        return NoContent();
    }

    private Task<bool> AllowedAsync(int id, int companyId, int branchId, int selfId) =>
        _db.Personeller.AnyAsync(x => x.Id == id && x.Id != selfId && x.CompanyId == companyId && x.BranchId == branchId && x.AktifMi);

    [HttpGet]
    public async Task<IActionResult> Ek(long id, bool onizle = false)
    {
        var scope = await _scope.GetAsync();
        if (scope == null) return Forbid();
        var attachment = await _db.MesajEkleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (attachment == null) return NotFound();
        bool allowed;
        if (attachment.OzelMesajId.HasValue)
        {
            allowed = await _db.OzelMesajlar.AsNoTracking().AnyAsync(x => x.Id == attachment.OzelMesajId &&
                x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId &&
                (x.GonderenPersonelId == scope.PersonelId || x.AliciPersonelId == scope.PersonelId));
        }
        else
        {
            allowed = await (from msg in _db.MesajGrubuMesajlari.AsNoTracking()
                join chatGroup in _db.MesajGruplari.AsNoTracking() on msg.GrupId equals chatGroup.Id
                join membership in _db.MesajGrubuUyelikleri.AsNoTracking() on chatGroup.Id equals membership.GrupId
                where msg.Id == attachment.GrupMesajiId && chatGroup.CompanyId == scope.ActiveCompanyId &&
                    chatGroup.BranchId == scope.ActiveBranchId && membership.PersonelId == scope.PersonelId &&
                    membership.KabulUtc != null && membership.RedUtc == null && msg.GonderimUtc >= membership.KabulUtc
                select msg.Id).AnyAsync();
        }
        if (!allowed) return NotFound();
        var path = _attachments.Resolve(attachment.DepoAdi);
        if (path == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if (onizle && (attachment.IcerikTuru.StartsWith("image/") ||
            attachment.IcerikTuru.StartsWith("video/") || attachment.IcerikTuru == "application/pdf"))
        {
            Response.Headers["Content-Security-Policy"] = "sandbox";
            return File(stream, attachment.IcerikTuru, enableRangeProcessing: true);
        }
        return File(stream, attachment.IcerikTuru, attachment.DosyaAdi, enableRangeProcessing: true);
    }

    private async Task<Dictionary<long, IReadOnlyList<MessageAttachmentItem>>> LoadAttachmentsAsync(IEnumerable<long> messageIds, bool group)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0) return new();
        var rows = await _db.MesajEkleri.AsNoTracking()
            .Where(x => group ? x.GrupMesajiId.HasValue && ids.Contains(x.GrupMesajiId.Value)
                              : x.OzelMesajId.HasValue && ids.Contains(x.OzelMesajId.Value))
            .Select(x => new { x.Id, x.OzelMesajId, x.GrupMesajiId, x.DosyaAdi, x.IcerikTuru, x.Boyut })
            .ToListAsync();
        return rows.GroupBy(x => group ? x.GrupMesajiId!.Value : x.OzelMesajId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MessageAttachmentItem>)g.Select(x =>
                new MessageAttachmentItem(x.Id, x.DosyaAdi, x.IcerikTuru, x.Boyut)).ToList());
    }

    private async Task<(List<MesajEki> Items, string? Error)> SaveAttachmentsAsync(IReadOnlyList<IFormFile> files)
    {
        var saved = new List<MesajEki>();
        try
        {
            foreach (var file in files)
                saved.Add(await _attachments.SaveAsync(file, HttpContext.RequestAborted));
            return (saved, null);
        }
        catch (InvalidDataException ex)
        {
            DeleteFiles(saved);
            return (new(), ex.Message);
        }
        catch
        {
            DeleteFiles(saved);
            throw;
        }
    }

    private void DeleteFiles(IEnumerable<MesajEki> items)
    {
        foreach (var item in items)
        {
            try { _attachments.Delete(item.DepoAdi); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _logger.LogWarning(ex, "Mesaj dosyası temizlenemedi: {Id}", item.Id); }
        }
    }

    private void DeleteFileNames(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            try { _attachments.Delete(name); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _logger.LogWarning(ex, "Mesaj dosyası temizlenemedi: {Name}", name); }
        }
    }
}

public sealed record MessageAttachmentItem(long Id, string DosyaAdi, string IcerikTuru, long Boyut);

public sealed class MesajKisi
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = "";
    public string SicilNo { get; set; } = "";
    public int? BranchId { get; set; }
    public int Okunmamis { get; set; }
    public string? SonMesaj { get; set; }
    public DateTime? SonMesajUtc { get; set; }
    public bool SonMesajBenden { get; set; }
    public bool Cevrimici { get; set; }
}

public sealed class MesajSayfaModel
{
    public List<MesajKisi> Kisiler { get; set; } = new();
    public int? SeciliKisiId { get; set; }
    public int AktifSubeId { get; set; }
    public int BenimId { get; set; }
    public List<MesajGrupSatiri> Gruplar { get; set; } = new();
    public int? SeciliGrupId { get; set; }
}

public sealed class MesajGrupSatiri
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public DateTime? SonMesajUtc { get; set; }
    public bool KurucuMu { get; set; }
}
