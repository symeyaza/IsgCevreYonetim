using System.Security.Cryptography;
using System.Text;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using IsgCevreYonetim.Web.Services;
using IsgCevreYonetim.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using QRCoder;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class TehlikeliIsController : Controller
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".mp4", ".mov", ".avi" };
    private const long MaxFileSize = 50L * 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly IUserScopeService _scopeService;
    private readonly IWebHostEnvironment _environment;
    private readonly IBildirimService _bildirimler;

    public TehlikeliIsController(ApplicationDbContext db, IUserScopeService scopeService, IWebHostEnvironment environment, IBildirimService bildirimler)
    {
        _db = db;
        _scopeService = scopeService;
        _environment = environment;
        _bildirimler = bildirimler;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string searchTerm = "", int? durumId = null, int page = 1, int pageSize = 10)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        var query = _db.TehlikeliIsler.AsNoTracking().AsSplitQuery()
            // Liste ekranında kullanılmayan ana kişiler, eski tekil tehlike sınıfı,
            // dosyalar, maddeler ve denetim zinciri özellikle yüklenmez.
            .Include(x => x.CalismaYapacakBirim).Include(x => x.TaseronFirma).Include(x => x.IsDurumu)
            .Include(x => x.Gunler).ThenInclude(x => x.TehlikeSiniflari).ThenInclude(x => x.TehlikeSinifi)
            .Include(x => x.Gunler).ThenInclude(x => x.Kisiler)
            .Where(x => x.BranchId == scope.ActiveBranchId);
        searchTerm = (searchTerm ?? string.Empty).Trim();
        if (searchTerm.Length > 0)
            query = query.Where(x => x.CalismaYapilacakYer.Contains(searchTerm) || x.YapilacakIsAciklamasi.Contains(searchTerm));
        if (durumId.HasValue) query = query.Where(x => x.IsDurumuId == durumId.Value);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        ViewBag.Durumlar = await _db.IsDurumlari.AsNoTracking().OrderBy(x => x.Sira).ToListAsync();
        ViewBag.SearchTerm = searchTerm;
        ViewBag.DurumId = durumId;
        ViewBag.Pagination = new PaginatedResult<object> { TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
        return View(await query.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Saat)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Rapor(int? yil = null, int? birimId = null, int? durumId = null, int sayfa = 1)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        sayfa = Math.Max(1, sayfa);
        var currentYear = DateTime.Today.Year;
        var selectedYear = (yil is >= 1900 and <= 9998) && yil <= currentYear ? yil.Value : currentYear;
        var query = TehlikeliRaporSorgusu(scope.ActiveBranchId, selectedYear, birimId, durumId);
        var summary = await query.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Completed = g.Count(x => x.IsDurumu != null && x.IsDurumu.Tamamlandi),
            Workers = g.Sum(x => x.CalisanPersonelSayisi)
        }).FirstOrDefaultAsync();
        sayfa = Math.Min(sayfa, Math.Max(1, (int)Math.Ceiling((summary?.Total ?? 0) / 25d)));
        var monthly = await query.GroupBy(x => x.Tarih.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() }).ToListAsync();
        var statuses = await query.GroupBy(x => x.IsDurumu != null ? x.IsDurumu.Ad : "Atanmamış")
            .OrderByDescending(g => g.Count()).Select(g => new RaporDagilimi(g.Key, g.Count())).ToListAsync();
        var dailyControls = await _db.TehlikeliIsGunleri.AsNoTracking()
            .CountAsync(g => g.TehlikeliIs != null && g.TehlikeliIs.BranchId == scope.ActiveBranchId &&
                g.TehlikeliIs.Tarih >= new DateTime(selectedYear, 1, 1) && g.TehlikeliIs.Tarih < new DateTime(selectedYear + 1, 1, 1) &&
                (!birimId.HasValue || g.TehlikeliIs.CalismaYapacakBirimId == birimId.Value) &&
                (!durumId.HasValue || g.TehlikeliIs.IsDurumuId == durumId.Value));
        var hazards = await _db.TehlikeliIsGunTehlikeSiniflari.AsNoTracking()
            .Where(g => g.Gun != null && g.Gun.TehlikeliIs != null && g.Gun.TehlikeliIs.BranchId == scope.ActiveBranchId &&
                g.Gun.TehlikeliIs.Tarih >= new DateTime(selectedYear, 1, 1) && g.Gun.TehlikeliIs.Tarih < new DateTime(selectedYear + 1, 1, 1) &&
                (!birimId.HasValue || g.Gun.TehlikeliIs.CalismaYapacakBirimId == birimId.Value) &&
                (!durumId.HasValue || g.Gun.TehlikeliIs.IsDurumuId == durumId.Value))
            .GroupBy(g => g.TehlikeSinifi != null ? g.TehlikeSinifi.Ad : "Atanmamış")
            .OrderByDescending(g => g.Count()).Select(g => new RaporDagilimi(g.Key, g.Count())).ToListAsync();
        var rows = await query.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Id)
            .Skip((sayfa - 1) * 25).Take(25)
            .Select(x => new TehlikeliIsRaporSatiri(x.Id, x.Tarih, x.YapilacakIsAciklamasi, x.CalismaYapilacakYer,
                x.TaseronFirma != null ? x.TaseronFirma.FirmaAdi : x.CalismaYapacakBirim != null ? x.CalismaYapacakBirim.DepartmentAdi : "—",
                x.IsDurumu != null ? x.IsDurumu.Ad : "—", x.CalisanPersonelSayisi, x.Gunler.Count))
            .ToListAsync();
        var firstDate = await _db.TehlikeliIsler.AsNoTracking().Where(x => x.BranchId == scope.ActiveBranchId)
            .MinAsync(x => (DateTime?)x.Tarih);
        var firstYear = Math.Clamp(firstDate?.Year ?? currentYear, 1900, currentYear);
        var model = new TehlikeliIsRaporViewModel
        {
            Sube = scope.ActiveBranchName, Yil = selectedYear, BirimId = birimId, DurumId = durumId,
            Toplam = summary?.Total ?? 0, Tamamlanan = summary?.Completed ?? 0,
            CalisanToplami = summary?.Workers ?? 0, GunKontrolSayisi = dailyControls,
            DurumDagilimi = statuses, TehlikeDagilimi = hazards, Satirlar = rows,
            Sayfa = sayfa, SayfaSayisi = (int)Math.Ceiling((summary?.Total ?? 0) / 25d),
            Yillar = Enumerable.Range(firstYear, currentYear - firstYear + 1).Reverse().ToList(),
            Birimler = await _db.Departments.AsNoTracking().Where(x => x.BranchId == scope.ActiveBranchId)
                .OrderBy(x => x.DepartmentAdi).Select(x => new RaporSecenegi(x.Id, x.DepartmentAdi)).ToListAsync(),
            Durumlar = await _db.IsDurumlari.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sira)
                .Select(x => new RaporSecenegi(x.Id, x.Ad)).ToListAsync()
        };
        if (!model.Yillar.Contains(selectedYear)) model.Yillar.Insert(0, selectedYear);
        foreach (var month in monthly) model.Aylik[month.Month - 1] = month.Count;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RaporExcel(int? yil = null, int? birimId = null, int? durumId = null)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        var year = (yil is >= 1900 and <= 9998) && yil <= DateTime.Today.Year ? yil.Value : DateTime.Today.Year;
        var reportQuery = TehlikeliRaporSorgusu(scope.ActiveBranchId, year, birimId, durumId);
        if (await reportQuery.Take(50001).CountAsync() > 50000) return BadRequest("Excel en fazla 50.000 kayıt içerir; filtreyi daraltın.");
        var rows = await reportQuery
            .OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Id)
            .Select(x => new TehlikeliIsRaporSatiri(x.Id, x.Tarih, x.YapilacakIsAciklamasi, x.CalismaYapilacakYer,
                x.TaseronFirma != null ? x.TaseronFirma.FirmaAdi : x.CalismaYapacakBirim != null ? x.CalismaYapacakBirim.DepartmentAdi : "—",
                x.IsDurumu != null ? x.IsDurumu.Ad : "—", x.CalisanPersonelSayisi, x.Gunler.Count))
            .ToListAsync();
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tehlikeli İş Raporu");
        var headers = new[] { "No", "Tarih", "Yapılacak İş", "Çalışma Yeri", "Birim", "Durum", "Çalışan", "Kontrol Günü" };
        for (var col = 0; col < headers.Length; col++) sheet.Cell(1, col + 1).Value = headers[col];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var n = i + 2;
            sheet.Cell(n, 1).Value = row.Id; sheet.Cell(n, 2).Value = row.Tarih;
            sheet.Cell(n, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            sheet.Cell(n, 3).Value = row.Is; sheet.Cell(n, 4).Value = row.Yer;
            sheet.Cell(n, 5).Value = row.Birim; sheet.Cell(n, 6).Value = row.Durum;
            sheet.Cell(n, 7).Value = row.Calisan; sheet.Cell(n, 8).Value = row.KayitliGun;
        }
        sheet.Range(1, 1, 1, 8).Style.Font.Bold = true;
        sheet.Columns(1, 8).AdjustToContents();
        using var output = new MemoryStream(); workbook.SaveAs(output);
        return File(output.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Tehlikeli_Is_Raporu.xlsx");
    }

    private IQueryable<TehlikeliIs> TehlikeliRaporSorgusu(int branchId, int year, int? birimId, int? durumId)
    {
        var start = new DateTime(year, 1, 1); var end = start.AddYears(1);
        var query = _db.TehlikeliIsler.AsNoTracking().Where(x => x.BranchId == branchId && x.Tarih >= start && x.Tarih < end);
        if (birimId.HasValue) query = query.Where(x => x.CalismaYapacakBirimId == birimId.Value);
        if (durumId.HasValue) query = query.Where(x => x.IsDurumuId == durumId.Value);
        return query;
    }

    [HttpGet]
    public async Task<IActionResult> Ekle()
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        await FillLookups(scope.ActiveBranchId);
        return View(new TehlikeliIsFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> PersonelAra(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var scope = await _scopeService.GetAsync();
        if (scope == null) return Json(Array.Empty<object>());
        var searchParts = term.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .ToList();
        var peopleQuery = _db.Personeller.AsNoTracking()
            .Where(x => x.BranchId == scope.ActiveBranchId && x.AktifMi && x.IsActive);
        foreach (var part in searchParts)
        {
            var searchPart = part;
            peopleQuery = peopleQuery.Where(x =>
                x.Ad.Contains(searchPart) ||
                x.Soyad.Contains(searchPart) ||
                x.SicilNo.Contains(searchPart));
        }
        var people = await peopleQuery
            .OrderBy(x => x.Ad).ThenBy(x => x.Soyad).Take(20)
            .Select(x => new { id = x.Id, text = x.Ad + " " + x.Soyad + " (" + x.SicilNo + ")", fullName = x.Ad + " " + x.Soyad })
            .ToListAsync();
        return Json(people);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(TehlikeliIsFormViewModel model)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        await ValidateForm(model, scope.ActiveBranchId);
        var initialStatusId = await _db.IsDurumlari.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sira).Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (!initialStatusId.HasValue) ModelState.AddModelError("", "Aktif iş durumu bulunamadı.");
        if (!ModelState.IsValid) { await FillLookups(scope.ActiveBranchId, MainSelectedPersonIds(model)); return View(model); }

        var entity = new TehlikeliIs
        {
            Tarih = model.Tarih.Date, Saat = model.Saat, BranchId = scope.ActiveBranchId,
            CalismaYapacakBirimId = model.CalismaKaynagi == "Dahili" ? model.CalismaYapacakBirimId : null,
            TaseronFirmaId = model.CalismaKaynagi == "Taseron" ? model.TaseronFirmaId : null,
            TaseronYetkiliId = model.CalismaKaynagi == "Taseron" ? model.TaseronYetkiliId : null,
            CalismaYapilacakYer = model.CalismaYapilacakYer.Trim(),
            YapilacakIsAciklamasi = model.YapilacakIsAciklamasi.Trim(),
            CalisanPersonelSayisi = 0,
            TehlikeSinifiId = null, IsDurumuId = initialStatusId!.Value,
            IsiYaptiranPersonelId = model.IsiYaptiranPersonelId!.Value,
            FirmaSorumlusuPersonelId = model.CalismaKaynagi == "Dahili" ? model.FirmaSorumlusuPersonelId : null,
            // Ana formda günlük roller gösterilmez. İlk değerler mevcut zorunlu
            // kolonlarla geriye uyumluluk için ana sorumlulardan alınır.
            KontrolEdenPersonelId = model.IsiYaptiranPersonelId!.Value,
            OnaylayanPersonelId = model.FirmaSorumlusuPersonelId ?? model.IsiYaptiranPersonelId!.Value,
            TasaronFirmaUnvani = model.CalismaKaynagi == "Taseron" ? await _db.TaseronFirmalar.Where(x => x.Id == model.TaseronFirmaId && x.BranchId == scope.ActiveBranchId).Select(x => x.FirmaAdi).FirstAsync() : null,
            Aciklama = null, CreatedDate = DateTime.Now, IsActive = true
        };
        _db.TehlikeliIsler.Add(entity);
        await _db.SaveChangesAsync();
        await AddAudit(entity.Id, null, scope.PersonelId, "İş oluşturuldu", "Tehlikeli iş kaydı oluşturuldu.");
        await _db.SaveChangesAsync();
        TempData["ToastrSuccess"] = "Tehlikeli iş kaydı başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = entity.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Duzenle(int id)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await _db.TehlikeliIsler.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        await FillLookups(scope.ActiveBranchId, new[] { (int?)item.IsiYaptiranPersonelId, item.FirmaSorumlusuPersonelId }.Where(x => x.HasValue).Select(x => x!.Value));
        return View(new TehlikeliIsFormViewModel
        {
            Id = item.Id, Tarih = item.Tarih, Saat = item.Saat, CalismaKaynagi = item.TaseronFirmaId.HasValue ? "Taseron" : "Dahili", CalismaYapacakBirimId = item.CalismaYapacakBirimId,
            TaseronFirmaId = item.TaseronFirmaId, TaseronYetkiliId = item.TaseronYetkiliId,
            CalismaYapilacakYer = item.CalismaYapilacakYer, YapilacakIsAciklamasi = item.YapilacakIsAciklamasi,
            IsiYaptiranPersonelId = item.IsiYaptiranPersonelId,
            FirmaSorumlusuPersonelId = item.FirmaSorumlusuPersonelId,
            TasaronFirmaAdi = item.TasaronFirmaUnvani
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(TehlikeliIsFormViewModel model)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await _db.TehlikeliIsler.FirstOrDefaultAsync(x => x.Id == model.Id && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        await ValidateForm(model, scope.ActiveBranchId);
        if ((item.TaseronFirmaId.HasValue != (model.CalismaKaynagi == "Taseron") ||
             (item.TaseronFirmaId.HasValue && item.TaseronFirmaId != model.TaseronFirmaId)) &&
            await _db.TehlikeliIsGunleri.AnyAsync(x => x.TehlikeliIsId == item.Id))
            ModelState.AddModelError(nameof(model.CalismaKaynagi), "Günlük kayıtlar bulunan işin çalışma kaynağı veya taşeron firması değiştirilemez.");
        if (!ModelState.IsValid)
        {
            await FillLookups(scope.ActiveBranchId, MainSelectedPersonIds(model));
            return View(model);
        }
        var oldStartDate = item.Tarih.Date;
        item.Tarih = model.Tarih.Date; item.Saat = model.Saat; item.CalismaYapacakBirimId = model.CalismaKaynagi == "Dahili" ? model.CalismaYapacakBirimId : null;
        item.TaseronFirmaId = model.CalismaKaynagi == "Taseron" ? model.TaseronFirmaId : null;
        item.CalismaYapilacakYer = model.CalismaYapilacakYer.Trim(); item.YapilacakIsAciklamasi = model.YapilacakIsAciklamasi.Trim();
        if (item.IsiYaptiranPersonelId != model.IsiYaptiranPersonelId!.Value)
        {
            item.IsiYaptiranImzaYolu = null; item.IsiYaptiranImzaSha256 = null;
        }
        if (item.FirmaSorumlusuPersonelId != model.FirmaSorumlusuPersonelId || item.TaseronYetkiliId != model.TaseronYetkiliId)
        {
            item.FirmaSorumlusuImzaYolu = null; item.FirmaSorumlusuImzaSha256 = null;
        }
        item.IsiYaptiranPersonelId = model.IsiYaptiranPersonelId.Value;
        item.FirmaSorumlusuPersonelId = model.CalismaKaynagi == "Dahili" ? model.FirmaSorumlusuPersonelId : null;
        item.TaseronYetkiliId = model.CalismaKaynagi == "Taseron" ? model.TaseronYetkiliId : null;
        item.OnaylayanPersonelId = item.FirmaSorumlusuPersonelId ?? item.IsiYaptiranPersonelId;
        if (model.CalismaKaynagi == "Taseron") item.TasaronFirmaUnvani = await _db.TaseronFirmalar.Where(x => x.Id == model.TaseronFirmaId && x.BranchId == scope.ActiveBranchId).Select(x => x.FirmaAdi).FirstAsync();
        item.Aciklama = null; item.UpdatedDate = DateTime.Now;
        var kayitliGunler = await _db.TehlikeliIsGunleri.Where(x => x.TehlikeliIsId == item.Id).ToListAsync();
        foreach (var day in kayitliGunler)
        {
            if (oldStartDate != model.Tarih.Date) day.Tarih = model.Tarih.Date.AddDays(day.GunNo - 1);
            day.UpdatedDate = DateTime.Now;
        }
        await AddAudit(item.Id, null, scope.PersonelId, "İş güncellendi", "Ana iş bilgileri güncellendi.");
        await _db.SaveChangesAsync();
        TempData["ToastrSuccess"] = "Kayıt güncellendi.";
        return RedirectToAction(nameof(Duzenle), new { id = item.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Detay(int id)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await DetailQuery().FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        var selectedPersonIds = new[] { (int?)item.IsiYaptiranPersonelId, item.FirmaSorumlusuPersonelId }.Where(x => x.HasValue).Select(x => x!.Value)
            .Concat(item.Kisiler.Where(x => x.PersonelId.HasValue).Select(x => x.PersonelId!.Value))
            .Concat(item.Gunler.SelectMany(x => new[] { x.KontrolEdenPersonelId, x.OnaylayanPersonelId }));
        ViewBag.Personeller = await GetSelectedPeople(scope.ActiveBranchId, selectedPersonIds);
        ViewBag.TaseronCalisanlar = item.TaseronFirmaId.HasValue
            ? await _db.TaseronKisiler.AsNoTracking().Where(x => x.TaseronFirmaId == item.TaseronFirmaId && x.BranchId == scope.ActiveBranchId && x.CalisanMi && x.IsActive).OrderBy(x => x.AdSoyad).ToListAsync()
            : new List<TaseronKisi>();
        ViewBag.CurrentPersonelId = scope.PersonelId;
        ViewBag.TehlikeSiniflari = await _db.TehlikeSiniflari.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sira).ToListAsync();
        var maddeler = await _db.TehlikeSinifiMaddeleri.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.TehlikeSinifiId).ThenBy(x => x.Sira).ToListAsync();
        return View("Detay", new TehlikeliIsDetayViewModel { Is = item, Maddeler = maddeler });
    }

    [HttpGet]
    public async Task<IActionResult> DenetimZinciri(int id)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await AuditQuery().FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> AnaImzalariKaydet(TehlikeliIsAnaImzaViewModel model)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await _db.TehlikeliIsler.Include(x => x.Kisiler)
            .FirstOrDefaultAsync(x => x.Id == model.TehlikeliIsId && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();

        var isiYaptiran = await SaveCanvasSignature(item.Id, null, "isi-yaptiran", model.IsiYaptiranImzaData);
        if (isiYaptiran.HasValue) { item.IsiYaptiranImzaYolu = isiYaptiran.Value.Path; item.IsiYaptiranImzaSha256 = isiYaptiran.Value.Hash; }
        var firma = await SaveCanvasSignature(item.Id, null, "firma-sorumlusu", model.FirmaSorumlusuImzaData);
        if (firma.HasValue) { item.FirmaSorumlusuImzaYolu = firma.Value.Path; item.FirmaSorumlusuImzaSha256 = firma.Value.Hash; }

        var anaKisiler = item.Kisiler.Where(x => x.TehlikeliIsGunId == null).ToDictionary(x => x.Id);
        foreach (var pair in model.KisiImzalari.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            if (!anaKisiler.TryGetValue(pair.Key, out var kisi)) continue;
            var imza = await SaveCanvasSignature(item.Id, null, $"isi-yapan-{kisi.Id}", pair.Value);
            if (!imza.HasValue) continue;
            kisi.ImzaYolu = imza.Value.Path; kisi.ImzaSha256 = imza.Value.Hash;
        }
        item.UpdatedDate = DateTime.Now;
        await AddAudit(item.Id, null, scope.PersonelId, "Ana imzalar kaydedildi", "İşi yaptıran, firma sorumlusu ve işi yapan kişilerin imzaları güncellendi.");
        await _db.SaveChangesAsync();
        TempData["ToastrSuccess"] = "Ana çalışma imzaları kaydedildi.";
        return RedirectToAction(nameof(Duzenle), new { id = item.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(110_000_000)]
    public async Task<IActionResult> GunKaydet(TehlikeliIsGunViewModel model)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await _db.TehlikeliIsler.Include(x => x.IsDurumu).FirstOrDefaultAsync(x => x.Id == model.TehlikeliIsId && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        if (model.GunNo < 1 || model.GunNo > 7) ModelState.AddModelError(nameof(model.GunNo), "İş en fazla 7 gün olabilir.");
        var expectedDate = model.GunNo is >= 1 and <= 7 ? item.Tarih.Date.AddDays(model.GunNo - 1) : item.Tarih.Date;
        if (model.GunNo is >= 1 and <= 7 && model.Tarih.Date != expectedDate)
            ModelState.AddModelError(nameof(model.Tarih), "Gün tarihi başlangıç tarihine göre otomatik belirlenir ve değiştirilemez.");
        foreach (var file in model.Dosyalar ?? new List<IFormFile>()) ValidateFile(file, false);
        var classIds = model.SeciliTehlikeSinifiIds.Distinct().ToList();
        if (classIds.Count == 0) ModelState.AddModelError(nameof(model.SeciliTehlikeSinifiIds), "En az bir tehlike sınıfı seçilmelidir.");
        var validClassIds = await _db.TehlikeSiniflari.Where(x => x.IsActive && classIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
        if (validClassIds.Count != classIds.Count) ModelState.AddModelError(nameof(model.SeciliTehlikeSinifiIds), "Geçersiz tehlike sınıfı seçimi var.");
        // Yüksekte çalışma ve kazı için operatörler aynı tehlikeli işte ayrı kişiler olmalıdır.
        var heightSelected = await _db.TehlikeSiniflari.AnyAsync(c => validClassIds.Contains(c.Id) && c.Ad.Contains("Yüksek"));
        var excavationSelected = await _db.TehlikeSiniflari.AnyAsync(c => validClassIds.Contains(c.Id) && c.Ad.Contains("Kaz"));
        var heightOperator = Clean(model.OperatorAdiSoyadi);
        var excavationOperator = Clean(model.KaziOperatorAdiSoyadi);
        if (heightSelected && excavationSelected && !string.IsNullOrWhiteSpace(heightOperator) &&
            string.Equals(heightOperator, excavationOperator, StringComparison.CurrentCultureIgnoreCase))
            ModelState.AddModelError(nameof(model.KaziOperatorAdiSoyadi), "Yüksekte çalışma ve kazı operatörü aynı kişi olamaz.");
        // Diğer günlerde karşı çalışma türünde kullanılan operatörle çakışmayı da engelle.
        var otherDays = await _db.TehlikeliIsGunleri.AsNoTracking()
            .Where(g => g.TehlikeliIsId == item.Id && g.GunNo != model.GunNo)
            .Select(g => new { g.OperatorAdiSoyadi, g.KaziOperatorAdiSoyadi }).ToListAsync();
        if (heightSelected && !string.IsNullOrWhiteSpace(heightOperator) && otherDays.Any(g =>
            string.Equals(g.KaziOperatorAdiSoyadi, heightOperator, StringComparison.CurrentCultureIgnoreCase)))
            ModelState.AddModelError(nameof(model.OperatorAdiSoyadi), "Bu operatör aynı tehlikeli işin başka bir gününde kazı operatörü olarak seçilmiş.");
        if (excavationSelected && !string.IsNullOrWhiteSpace(excavationOperator) && otherDays.Any(g =>
            string.Equals(g.OperatorAdiSoyadi, excavationOperator, StringComparison.CurrentCultureIgnoreCase)))
            ModelState.AddModelError(nameof(model.KaziOperatorAdiSoyadi), "Bu operatör aynı tehlikeli işin başka bir gününde yüksekte çalışma operatörü olarak seçilmiş.");
        var workerIds = model.IsiYapanPersonelIds.Distinct().ToList();
        var contractorIds = model.TaseronCalisanIds.Distinct().ToList();
        if (item.TaseronFirmaId.HasValue)
        {
            if (workerIds.Count > 0 || !string.IsNullOrWhiteSpace(model.DisCalisanlar)) ModelState.AddModelError("", "Taşeron işinde yalnızca seçilen firmaya kayıtlı çalışanlar kullanılabilir.");
            if (await _db.TaseronKisiler.CountAsync(x => contractorIds.Contains(x.Id) && x.TaseronFirmaId == item.TaseronFirmaId && x.BranchId == scope.ActiveBranchId && x.CalisanMi && x.IsActive) != contractorIds.Count)
                ModelState.AddModelError(nameof(model.TaseronCalisanIds), "Çalışanlar seçilen taşeron firmaya ait olmalıdır.");
        }
        else if (contractorIds.Count > 0 || !string.IsNullOrWhiteSpace(model.DisCalisanlar)) ModelState.AddModelError("", "Dahili iş için yalnızca şube personeli seçilebilir.");
        if (workerIds.Count > 0 && await _db.Personeller.CountAsync(x => workerIds.Contains(x.Id) && x.BranchId == scope.ActiveBranchId && x.AktifMi && x.IsActive) != workerIds.Count)
            ModelState.AddModelError(nameof(model.IsiYapanPersonelIds), "İşi yapan personeller aktif şubeden seçilmelidir.");
        if (workerIds.Count + contractorIds.Count == 0) ModelState.AddModelError(nameof(model.IsiYapanPersonelIds), "Bu gün için en az bir çalışan seçilmelidir.");
        if (workerIds.Count + contractorIds.Count > 5) ModelState.AddModelError(nameof(model.IsiYapanPersonelIds), "Bir günde en fazla 5 çalışan seçilebilir.");
        var gun = await _db.TehlikeliIsGunleri.Include(x => x.Maddeler).Include(x => x.TehlikeSiniflari)
            .FirstOrDefaultAsync(x => x.TehlikeliIsId == item.Id && x.GunNo == model.GunNo);
        var selectedPeople = new[] { model.KontrolEdenPersonelId, model.OnaylayanPersonelId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (selectedPeople.Count > 0 && await _db.Personeller.AsNoTracking()
            .CountAsync(x => selectedPeople.Contains(x.Id) && x.BranchId == scope.ActiveBranchId && x.AktifMi && x.IsActive) != selectedPeople.Count)
            ModelState.AddModelError("", "Kontrol Eden ve Onaylayan aktif şubeden seçilmelidir.");
        if (!ModelState.IsValid) { TempData["ToastrError"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)); return RedirectToAction(nameof(Detay), new { id = item.Id }); }

        if (gun == null)
        {
            gun = new TehlikeliIsGun
            {
                TehlikeliIsId = item.Id, GunNo = model.GunNo, Tarih = model.Tarih.Date,
                KontrolEdenPersonelId = model.KontrolEdenPersonelId!.Value,
                OnaylayanPersonelId = model.OnaylayanPersonelId!.Value,
                CreatedDate = DateTime.Now
            };
            _db.TehlikeliIsGunleri.Add(gun);
        }
        if (gun.Id > 0 && gun.KontrolEdenPersonelId != model.KontrolEdenPersonelId!.Value)
        {
            gun.KontrolImzaYolu = null; gun.KontrolImzaSha256 = null;
        }
        if (gun.Id > 0 && gun.OnaylayanPersonelId != model.OnaylayanPersonelId!.Value)
        {
            gun.OnayImzaYolu = null; gun.OnayImzaSha256 = null;
        }
        gun.Tarih = expectedDate; gun.KontrolEdenPersonelId = model.KontrolEdenPersonelId!.Value; gun.OnaylayanPersonelId = model.OnaylayanPersonelId!.Value;
        gun.Aciklama = Clean(model.Aciklama); gun.TasaronFirmaUnvani = item.TasaronFirmaUnvani;
        gun.VincPlakasi = heightSelected ? Clean(model.VincPlakasi) : null;
        gun.OperatorAdiSoyadi = heightSelected ? heightOperator : null;
        gun.KaziIsMakinesi = excavationSelected ? Clean(model.KaziIsMakinesi) : null;
        gun.KaziOperatorAdiSoyadi = excavationSelected ? excavationOperator : null;
        gun.UpdatedDate = DateTime.Now;
        _db.TehlikeliIsGunMaddeleri.RemoveRange(gun.Maddeler);
        _db.TehlikeliIsGunTehlikeSiniflari.RemoveRange(gun.TehlikeSiniflari);
        foreach (var classId in validClassIds)
            gun.TehlikeSiniflari.Add(new TehlikeliIsGunTehlikeSinifi { Gun = gun, TehlikeSinifiId = classId, CreatedDate = DateTime.Now, IsActive = true });
        var validIds = await _db.TehlikeSinifiMaddeleri.Where(x => validClassIds.Contains(x.TehlikeSinifiId) && model.SeciliMaddeIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
        foreach (var mid in validIds.Distinct())
            gun.Maddeler.Add(new TehlikeliIsGunMadde { Gun = gun, MaddeId = mid, Uygun = true, CreatedDate = DateTime.Now, IsActive = true });
        await SaveFiles(item.Id, gun, model.Dosyalar);
        await _db.SaveChangesAsync();
        await ReplaceWorkers(item.Id, gun.Id, workerIds, contractorIds, item.TaseronFirmaId, item.TasaronFirmaUnvani, scope.ActiveBranchId);
        var dayCounts = await _db.TehlikeliIsKisileri.Where(x => x.TehlikeliIsId == item.Id && x.TehlikeliIsGunId != null)
            .GroupBy(x => x.TehlikeliIsGunId).Select(x => x.Count()).ToListAsync();
        item.CalisanPersonelSayisi = Math.Max(workerIds.Count + contractorIds.Count, dayCounts.DefaultIfEmpty(0).Max());
        await AddAudit(item.Id, gun.Id > 0 ? gun.Id : null, scope.PersonelId, "Gün kaydedildi", $"{model.GunNo}. gün tehlike sınıfları, kontrol maddeleri ve dosyaları kaydedildi.");
        await _db.SaveChangesAsync();
        TempData["ToastrSuccess"] = $"{model.GunNo}. gün kaydedildi.";
        return RedirectToAction(nameof(Detay), new { id = item.Id });
    }

    [HttpGet]
    public async Task<IActionResult> ExcelIndir(int id)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await ExcelQuery().FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        await using var input = OpenTehlikeliIsExcelTemplate();
        if (input == null) return NotFound("Tehlikeli iş Excel şablonu fiziksel dosyada veya uygulama kaynağında bulunamadı.");
        var workbook = new XSSFWorkbook(input); var sheet = workbook.GetSheetAt(0);
        var drawing = (XSSFDrawing)sheet.CreateDrawingPatriarch();
        EnsureFiveWorkerLayout(sheet);
        // Şablondaki hücre adresleri A1 biçiminde açıkça kullanılır. Böylece
        // NPOI'nin sıfır tabanlı satır/sütun hesabından kaynaklanan kaymalar önlenir.
        Set(sheet, "I5", string.Join(" / ", new[]
        {
            item.TasaronFirmaUnvani,
            item.CalismaYapacakBirim?.DepartmentAdi,
            item.Branch?.BranchAdi
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.CurrentCultureIgnoreCase)));
        Set(sheet, "I6", item.CalismaYapilacakYer);
        Set(sheet, "I7", $"{item.Tarih:dd.MM.yyyy} / {item.Saat:hh\\:mm}");
        Set(sheet, "F8", item.YapilacakIsAciklamasi);

        // Yeni form yapısında işi yapanlar günlük kayıtlarda tutulur. Eski
        // kayıtlarda ana kişiler varsa onları, yoksa ilk dolu günün çalışanlarını kullan.
        var anaIsiYapanlar = item.Kisiler.Where(x => x.TehlikeliIsGunId == null).OrderBy(x => x.Id).ToList();
        var ilkDoluGunKisileri = item.Gunler.OrderBy(x => x.GunNo)
            .Select(x => x.Kisiler.OrderBy(k => k.Id).ToList())
            .FirstOrDefault(x => x.Count > 0) ?? new List<TehlikeliIsKisi>();
        var isiYapanlar = (anaIsiYapanlar.Count > 0 ? anaIsiYapanlar : ilkDoluGunKisileri)
            .GroupBy(x => x.PersonelId.HasValue ? $"p:{x.PersonelId}" : $"d:{x.AdSoyad.Trim().ToUpperInvariant()}")
            .Select(x => x.First()).Take(5).ToList();
        Set(sheet, "F10", Math.Max(item.CalisanPersonelSayisi, isiYapanlar.Count).ToString());
        Set(sheet, "C18", item.IsiYaptiranPersonel?.FullName);
        Set(sheet, "Q18", item.TaseronYetkili?.AdSoyad ?? item.FirmaSorumlusuPersonel?.FullName);
        AddSignatureToExcel(workbook, drawing, item.IsiYaptiranImzaYolu, 2, 18, 20, 5);
        AddSignatureToExcel(workbook, drawing, item.FirmaSorumlusuImzaYolu, 16, 18, 20, 20);
        if (string.IsNullOrWhiteSpace(item.IsiYaptiranImzaYolu)) Set(sheet, "C19", "");
        if (string.IsNullOrWhiteSpace(item.FirmaSorumlusuImzaYolu)) Set(sheet, "Q19", "");
        var workerColumns = new[]
        {
            (NameCell: "F17", From: 5, To: 7), (NameCell: "H17", From: 7, To: 9),
            (NameCell: "J17", From: 9, To: 11), (NameCell: "L17", From: 11, To: 13),
            (NameCell: "N17", From: 13, To: 16)
        };
        for (var i = 0; i < isiYapanlar.Count; i++)
        {
            var worker = isiYapanlar[i]; var area = workerColumns[i];
            Set(sheet, area.NameCell, worker.AdSoyad);
            AddSignatureToExcel(workbook, drawing, worker.ImzaYolu, area.From, 18, 20, area.To);
            if (string.IsNullOrWhiteSpace(worker.ImzaYolu)) Set(sheet, 18, area.From, "");
        }
        var aciklamalar = new[] { item.Aciklama }
            .Concat(item.Gunler.OrderBy(x => x.GunNo)
                .Where(x => !string.IsNullOrWhiteSpace(x.Aciklama))
                .Select(x => $"{x.GunNo}. Gün: {x.Aciklama}"));
        Set(sheet, "C69", string.Join(" | ", aciklamalar.Where(x => !string.IsNullOrWhiteSpace(x))));

        foreach (var gun in item.Gunler)
        {
            var dayColumn = 13 + gun.GunNo - 1;
            Set(sheet, 22, dayColumn, $"{gun.GunNo}. GÜN\n{gun.Tarih:dd.MM.yyyy}");
            var dayHeader = sheet.GetRow(22).GetCell(dayColumn);
            var dayHeaderStyle = workbook.CreateCellStyle();
            dayHeaderStyle.CloneStyleFrom(dayHeader.CellStyle); dayHeaderStyle.WrapText = true; dayHeader.CellStyle = dayHeaderStyle;
            foreach (var gm in gun.Maddeler.Where(x => x.Uygun && x.Madde?.ExcelSatirNo != null))
                Set(sheet, gm.Madde!.ExcelSatirNo!.Value - 1, dayColumn, "X");
            var kontrolMetni = gun.KontrolEdenPersonel?.FullName ?? "—";
            var onayMetni = gun.OnaylayanPersonel?.FullName ?? "—";
            Set(sheet, 67, dayColumn, kontrolMetni); // N68:T68 birleşik alanlarının üst hücresi
            Set(sheet, 72, dayColumn, onayMetni);    // N73:T73 birleşik alanlarının üst hücresi
            AddSignatureToExcel(workbook, drawing, gun.KontrolImzaYolu, dayColumn, 68, 72, dayColumn + 1);
            AddSignatureToExcel(workbook, drawing, gun.OnayImzaYolu, dayColumn, 73, 78, dayColumn + 1);
            // F49:M49 ve F55:M55 birleşik form alanlarıdır; N:T günlük kontrol işaretleme sütunlarına yazılmaz.
        }

        // Birleştirilmiş F:M alanında birden fazla gün varsa gün numarasıyla ayrıştır.
        var heightLines = item.Gunler.OrderBy(g => g.GunNo)
            .Where(g => !string.IsNullOrWhiteSpace(g.OperatorAdiSoyadi) || !string.IsNullOrWhiteSpace(g.VincPlakasi))
            .Select(g => $"{g.GunNo}. gün: " + string.Join(" / ", new[]
            {
                string.IsNullOrWhiteSpace(g.OperatorAdiSoyadi) ? null : $"Operatör: {g.OperatorAdiSoyadi}",
                string.IsNullOrWhiteSpace(g.VincPlakasi) ? null : $"Vinç: {g.VincPlakasi}"
            }.Where(s => s != null)));
        var excavationLines = item.Gunler.OrderBy(g => g.GunNo)
            .Where(g => !string.IsNullOrWhiteSpace(g.KaziOperatorAdiSoyadi) || !string.IsNullOrWhiteSpace(g.KaziIsMakinesi))
            .Select(g => $"{g.GunNo}. gün: " + string.Join(" / ", new[]
            {
                string.IsNullOrWhiteSpace(g.KaziOperatorAdiSoyadi) ? null : $"Operatör: {g.KaziOperatorAdiSoyadi}",
                string.IsNullOrWhiteSpace(g.KaziIsMakinesi) ? null : $"İş makinesi: {g.KaziIsMakinesi}"
            }.Where(s => s != null)));
        Set(sheet, "F49", string.Join("\n", heightLines));
        Set(sheet, "F55", string.Join("\n", excavationLines));
        foreach (var address in new[] { "F49", "F55" })
        {
            var cell = sheet.GetRow(new NPOI.SS.Util.CellReference(address).Row)
                .GetCell(new NPOI.SS.Util.CellReference(address).Col);
            var style = workbook.CreateCellStyle(); style.CloneStyleFrom(cell.CellStyle);
            style.WrapText = true; cell.CellStyle = style;
        }

        var qrUrl = Url.Action(nameof(Qr), "TehlikeliIs", new { kod = item.DogrulamaKodu }, Request.Scheme)!;
        using (var qrGenerator = new QRCodeGenerator())
        using (var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q))
        using (var qrCode = new PngByteQRCode(qrData))
        {
            var pictureIndex = workbook.AddPicture(qrCode.GetGraphic(8), NPOI.SS.UserModel.PictureType.PNG);
            var anchor = new XSSFClientAnchor { Col1 = 17, Row1 = 2, Col2 = 20, Row2 = 7 };
            drawing.CreatePicture(anchor, pictureIndex);
        }
        using var output = new MemoryStream(); workbook.Write(output, true);
        await AddAudit(item.Id, null, scope.PersonelId, "Excel indirildi", "QR kodlu tehlikeli iş çalışma izin formu oluşturuldu.");
        await _db.SaveChangesAsync();
        return File(output.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Tehlikeli_Is_{item.Id}_{item.Tarih:yyyyMMdd}.xlsx");
    }

    private Stream? OpenTehlikeliIsExcelTemplate()
    {
        const string fileName = "TehlikeliIsCalismaIzni.xlsx";
        var paths = new[]
        {
            Path.Combine(_environment.ContentRootPath, "Templates", fileName),
            Path.Combine(AppContext.BaseDirectory, "Templates", fileName)
        };

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            if (System.IO.File.Exists(path))
                return System.IO.File.OpenRead(path);

        // Paylaşımlı hosting Templates klasörünü atlamış olsa bile DLL içine
        // gömülen şablon sayesinde Excel üretimi devam eder.
        return typeof(TehlikeliIsController).Assembly.GetManifestResourceStream(
            "IsgCevreYonetim.Web.Templates.TehlikeliIsCalismaIzni.xlsx");
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Qr(string kod)
    {
        var item = await _db.TehlikeliIsler.AsNoTracking().FirstOrDefaultAsync(x => x.DogrulamaKodu == kod);
        return item == null ? NotFound() : RedirectToAction(nameof(Detay), new { id = item.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var scope = await _scopeService.GetAsync(); if (scope == null) return Forbid();
        var item = await _db.TehlikeliIsler.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
        if (item == null) return NotFound();
        item.IsDeleted = true; item.IsActive = false; item.UpdatedDate = DateTime.Now;
        await AddAudit(item.Id, null, scope.PersonelId, "İş arşivlendi", "Kayıt kullanıcı tarafından arşivlendi.");
        await _db.SaveChangesAsync();
        await _bildirimler.ReferansGrubunuSilAsync($"tehlikeli-is:{item.Id}:");
        TempData["ToastrSuccess"] = "Kayıt arşivlendi."; return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DosyaSil(int id)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return Forbid();
        var file = await _db.TehlikeliIsDosyalari
            .Include(x => x.TehlikeliIs)
            .FirstOrDefaultAsync(x => x.Id == id && x.TehlikeliIs != null && x.TehlikeliIs.BranchId == scope.ActiveBranchId);
        if (file == null) return NotFound();

        file.IsDeleted = true;
        file.IsActive = false;
        file.UpdatedDate = DateTime.Now;
        await AddAudit(file.TehlikeliIsId, file.TehlikeliIsGunId, scope.PersonelId,
            "Dosya silindi", $"Dosya #{file.Id} ({file.DosyaAdi}) silindi. SHA256: {file.Sha256}");
        await _db.SaveChangesAsync();

        // Yalnızca bu modülün yükleme dizinindeki dosyaları fiziksel olarak kaldır.
        var relative = file.DosyaYolu.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "tehlikeli-isler")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relative));
        try
        {
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }
        catch (IOException) { /* The database entry is already removed from the visible record. */ }
        catch (UnauthorizedAccessException) { /* The database entry is already removed from the visible record. */ }

        TempData["ToastrSuccess"] = "Dosya silindi.";
        var dayNumber = await _db.TehlikeliIsGunleri.Where(g => g.Id == file.TehlikeliIsGunId)
            .Select(g => g.GunNo).FirstOrDefaultAsync();
        return RedirectToAction(nameof(Detay), controllerName: null,
            routeValues: new { id = file.TehlikeliIsId }, fragment: $"gun-{dayNumber}");
    }

    private IQueryable<TehlikeliIs> DetailQuery() => _db.TehlikeliIsler.AsNoTracking().AsSplitQuery()
        .Include(x => x.CalismaYapacakBirim).Include(x => x.TaseronFirma).Include(x => x.TaseronYetkili)
        .Include(x => x.IsiYaptiranPersonel).Include(x => x.FirmaSorumlusuPersonel)
        .Include(x => x.Kisiler)
        .Include(x => x.Gunler).ThenInclude(x => x.KontrolEdenPersonel)
        .Include(x => x.Gunler).ThenInclude(x => x.OnaylayanPersonel)
        .Include(x => x.Gunler).ThenInclude(x => x.Kisiler)
        .Include(x => x.Gunler).ThenInclude(x => x.Maddeler)
        .Include(x => x.Gunler).ThenInclude(x => x.TehlikeSiniflari)
        .Include(x => x.Gunler).ThenInclude(x => x.Dosyalar);

    private IQueryable<TehlikeliIs> ExcelQuery() => _db.TehlikeliIsler.AsNoTracking().AsSplitQuery()
        .Include(x => x.Branch).Include(x => x.CalismaYapacakBirim).Include(x => x.TaseronFirma).Include(x => x.TaseronYetkili)
        .Include(x => x.IsiYaptiranPersonel).Include(x => x.FirmaSorumlusuPersonel)
        .Include(x => x.Kisiler)
        .Include(x => x.Gunler).ThenInclude(x => x.KontrolEdenPersonel)
        .Include(x => x.Gunler).ThenInclude(x => x.OnaylayanPersonel)
        .Include(x => x.Gunler).ThenInclude(x => x.Kisiler)
        .Include(x => x.Gunler).ThenInclude(x => x.Maddeler).ThenInclude(x => x.Madde);

    private IQueryable<TehlikeliIs> AuditQuery() => _db.TehlikeliIsler.AsNoTracking().AsSplitQuery()
        .Include(x => x.DenetimKayitlari).ThenInclude(x => x.Personel);


    private async Task FillLookups(int branchId, IEnumerable<int>? selectedPersonIds = null)
    {
        ViewBag.Birimler = await _db.Departments.AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive).OrderBy(x => x.DepartmentAdi).ToListAsync();
        ViewBag.TaseronFirmalar = await _db.TaseronFirmalar.AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive).OrderBy(x => x.FirmaAdi).ToListAsync();
        ViewBag.TaseronKisiler = await _db.TaseronKisiler.AsNoTracking().Where(x => x.BranchId == branchId && x.YetkiliMi && x.IsActive).OrderBy(x => x.AdSoyad).ToListAsync();
        ViewBag.TehlikeSiniflari = await _db.TehlikeSiniflari.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sira).ToListAsync();
        ViewBag.IsDurumlari = await _db.IsDurumlari.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sira).ToListAsync();
        ViewBag.Personeller = await GetSelectedPeople(branchId, selectedPersonIds);
    }

    private async Task<List<Personel>> GetSelectedPeople(int branchId, IEnumerable<int>? selectedPersonIds)
    {
        var ids = (selectedPersonIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
        return ids.Count == 0
            ? new List<Personel>()
            : await _db.Personeller.AsNoTracking().Where(x => x.BranchId == branchId && ids.Contains(x.Id)).OrderBy(x => x.Ad).ThenBy(x => x.Soyad).ToListAsync();
    }

    private static IEnumerable<int> MainSelectedPersonIds(TehlikeliIsFormViewModel model) =>
        new[] { model.IsiYaptiranPersonelId, model.FirmaSorumlusuPersonelId }
            .Where(x => x.HasValue).Select(x => x!.Value).Concat(model.IsiYapanPersonelIds);

    private async Task ValidateForm(TehlikeliIsFormViewModel model, int branchId)
    {
        if (model.CalismaKaynagi == "Dahili")
        {
            if (!model.CalismaYapacakBirimId.HasValue || !await _db.Departments.AnyAsync(x => x.Id == model.CalismaYapacakBirimId && x.BranchId == branchId && x.IsActive)) ModelState.AddModelError(nameof(model.CalismaYapacakBirimId), "Birim aktif şubeye ait olmalıdır.");
            if (!model.FirmaSorumlusuPersonelId.HasValue) ModelState.AddModelError(nameof(model.FirmaSorumlusuPersonelId), "Birim sorumlusu seçilmelidir.");
        }
        else if (model.CalismaKaynagi == "Taseron")
        {
            if (!model.TaseronFirmaId.HasValue || !await _db.TaseronFirmalar.AnyAsync(x => x.Id == model.TaseronFirmaId && x.BranchId == branchId && x.IsActive)) ModelState.AddModelError(nameof(model.TaseronFirmaId), "Firma aktif şubeye ait olmalıdır.");
            if (!model.TaseronYetkiliId.HasValue || !await _db.TaseronKisiler.AnyAsync(x => x.Id == model.TaseronYetkiliId && x.TaseronFirmaId == model.TaseronFirmaId && x.BranchId == branchId && x.YetkiliMi && x.IsActive)) ModelState.AddModelError(nameof(model.TaseronYetkiliId), "Yetkili seçilen firmaya ait olmalıdır.");
        }
        else ModelState.AddModelError(nameof(model.CalismaKaynagi), "Çalışma kaynağını seçiniz.");
        if (!model.IsiYaptiranPersonelId.HasValue) ModelState.AddModelError(nameof(model.IsiYaptiranPersonelId), "İşi yaptıran seçilmelidir.");
        var people = new[] { model.IsiYaptiranPersonelId, model.CalismaKaynagi == "Dahili" ? model.FirmaSorumlusuPersonelId : null }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (people.Count > 0 && await _db.Personeller.CountAsync(x => people.Contains(x.Id) && x.BranchId == branchId && x.AktifMi && x.IsActive) != people.Count)
            ModelState.AddModelError("", "Seçilen sorumlular aktif şubeye ait olmalıdır.");
    }

    private static void EnsureFiveWorkerLayout(NPOI.SS.UserModel.ISheet sheet)
    {
        var required = new[]
        {
            new CellRangeAddress(16, 17, 5, 6), new CellRangeAddress(18, 19, 5, 6),
            new CellRangeAddress(16, 17, 7, 8), new CellRangeAddress(18, 19, 7, 8),
            new CellRangeAddress(16, 17, 9, 10), new CellRangeAddress(18, 19, 9, 10),
            new CellRangeAddress(16, 17, 11, 12), new CellRangeAddress(18, 19, 11, 12),
            new CellRangeAddress(16, 17, 13, 15), new CellRangeAddress(18, 19, 13, 15)
        };
        var existing = Enumerable.Range(0, sheet.NumMergedRegions).Select(i => sheet.GetMergedRegion(i).FormatAsString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var region in required)
        {
            if (!existing.Contains(region.FormatAsString())) sheet.AddMergedRegion(region);
            ApplyWorkerBoxStyle(sheet, region, region.FirstRow == 18);
        }
    }

    private static void ApplyWorkerBoxStyle(NPOI.SS.UserModel.ISheet sheet, CellRangeAddress region, bool signatureBox)
    {
        for (var rowIndex = region.FirstRow; rowIndex <= region.LastRow; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            for (var columnIndex = region.FirstColumn; columnIndex <= region.LastColumn; columnIndex++)
            {
                var cell = row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
                var style = sheet.Workbook.CreateCellStyle();
                style.CloneStyleFrom(cell.CellStyle);
                style.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;
                style.VerticalAlignment = NPOI.SS.UserModel.VerticalAlignment.Center;
                style.WrapText = true;
                style.BorderTop = rowIndex == region.FirstRow ? NPOI.SS.UserModel.BorderStyle.Thin : NPOI.SS.UserModel.BorderStyle.None;
                style.BorderBottom = rowIndex == region.LastRow
                    ? signatureBox ? NPOI.SS.UserModel.BorderStyle.Medium : NPOI.SS.UserModel.BorderStyle.Thin
                    : NPOI.SS.UserModel.BorderStyle.None;
                style.BorderLeft = columnIndex == region.FirstColumn ? NPOI.SS.UserModel.BorderStyle.Thin : NPOI.SS.UserModel.BorderStyle.None;
                style.BorderRight = columnIndex == region.LastColumn ? NPOI.SS.UserModel.BorderStyle.Thin : NPOI.SS.UserModel.BorderStyle.None;
                cell.CellStyle = style;
            }
        }
    }

    private async Task ReplaceWorkers(int workId, int? dayId, IEnumerable<int> personIds, IEnumerable<int> contractorIds, int? firmId, string? firmName, int branchId)
    {
        var old = await _db.TehlikeliIsKisileri.Where(x => x.TehlikeliIsId == workId && x.TehlikeliIsGunId == dayId).ToListAsync();
        var internalIds = personIds.Distinct().ToHashSet();
        var externalIds = contractorIds.Distinct().ToHashSet();
        var removed = old.Where(x => x.PersonelId.HasValue ? !internalIds.Contains(x.PersonelId.Value)
            : x.TaseronKisiId.HasValue ? !externalIds.Contains(x.TaseronKisiId.Value) : false).ToList();
        _db.TehlikeliIsKisileri.RemoveRange(removed);
        var existingInternal = old.Except(removed).Where(x => x.PersonelId.HasValue).Select(x => x.PersonelId!.Value).ToHashSet();
        var existingExternal = old.Except(removed).Where(x => x.TaseronKisiId.HasValue).Select(x => x.TaseronKisiId!.Value).ToHashSet();
        var newInternal = internalIds.Except(existingInternal).ToList();
        var newExternal = externalIds.Except(existingExternal).ToList();
        var people = await _db.Personeller.AsNoTracking().Where(x => newInternal.Contains(x.Id) && x.BranchId == branchId).ToListAsync();
        var contractors = await _db.TaseronKisiler.AsNoTracking().Where(x => newExternal.Contains(x.Id) && x.TaseronFirmaId == firmId && x.BranchId == branchId).ToListAsync();
        _db.TehlikeliIsKisileri.AddRange(people.Select(x => new TehlikeliIsKisi { TehlikeliIsId = workId, TehlikeliIsGunId = dayId, PersonelId = x.Id, AdSoyad = x.FullName, Rol = "IsiYapan", CreatedDate = DateTime.Now, IsActive = true }));
        _db.TehlikeliIsKisileri.AddRange(contractors.Select(x => new TehlikeliIsKisi { TehlikeliIsId = workId, TehlikeliIsGunId = dayId, TaseronKisiId = x.Id, AdSoyad = x.AdSoyad, Firma = firmName, Rol = "IsiYapan", CreatedDate = DateTime.Now, IsActive = true }));
    }

    private async Task SaveFiles(int workId, TehlikeliIsGun gun, IEnumerable<IFormFile>? files)
    {
        foreach (var file in files ?? Array.Empty<IFormFile>())
        {
            if (file.Length == 0) continue;
            var saved = await SaveSingleFile(workId, -gun.GunNo, file, false);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
                ".pdf" => "application/pdf", ".mp4" => "video/mp4",
                ".mov" => "video/quicktime", ".avi" => "video/x-msvideo", _ => "application/octet-stream"
            };
            _db.TehlikeliIsDosyalari.Add(new TehlikeliIsDosya { TehlikeliIsId = workId, TehlikeliIsGunId = gun.Id, Gun = gun, DosyaAdi = Path.GetFileName(file.FileName), DosyaYolu = saved.Path, DosyaTipi = contentType, Sha256 = saved.Hash, DosyaBoyutu = file.Length, CreatedDate = DateTime.Now, IsActive = true });
        }
    }

    private async Task<(string Path, string Hash)> SaveSingleFile(int workId, int? dayId, IFormFile file, bool signed)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || file.Length > MaxFileSize) throw new InvalidOperationException("Dosya türü desteklenmiyor veya 50 MB sınırını aşıyor.");
        if (signed && ext is not ".pdf" and not ".jpg" and not ".jpeg" and not ".png") throw new InvalidOperationException("Islak imzalı belge PDF/JPG/PNG olmalıdır.");
        // ContentType istemciden gelir; gerçek dosya başlığını uzantıyla karşılaştır.
        var header = new byte[16];
        await using (var check = file.OpenReadStream())
        {
            var length = 0;
            while (length < header.Length)
            {
                var count = await check.ReadAsync(header.AsMemory(length));
                if (count == 0) break;
                length += count;
            }
        }
        var valid = ext switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".pdf" => header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".webp" => header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".mp4" or ".mov" => header.AsSpan(4, 4).SequenceEqual("ftyp"u8),
            ".avi" => header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("AVI "u8),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("Dosya içeriği seçilen türle uyuşmuyor.");
        var dayFolder = dayId is < 0 ? $"gun-{Math.Abs(dayId.Value)}" : dayId?.ToString() ?? "genel";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "tehlikeli-isler", workId.ToString(), dayFolder);
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext}"; var full = Path.Combine(folder, name);
        await using var source = file.OpenReadStream(); await using var target = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        using var sha = SHA256.Create();
        var buffer = new byte[81920]; int read;
        while ((read = await source.ReadAsync(buffer)) > 0) { await target.WriteAsync(buffer.AsMemory(0, read)); sha.TransformBlock(buffer, 0, read, null, 0); }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ($"/uploads/tehlikeli-isler/{workId}/{dayFolder}/{name}", Convert.ToHexString(sha.Hash!).ToLowerInvariant());
    }

    private async Task<(string Path, string Hash)?> SaveCanvasSignature(int workId, int? dayId, string type, string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        const string prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("İmza verisi geçersiz.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[prefix.Length..]); }
        catch (FormatException) { throw new InvalidOperationException("İmza verisi okunamadı."); }
        if (bytes.Length is < 100 or > 2_000_000)
            throw new InvalidOperationException("İmza boyutu geçersiz.");
        var signatureFolder = dayId is < 0 ? $"gun-{Math.Abs(dayId.Value)}" : dayId?.ToString() ?? "ana";
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "tehlikeli-isler", workId.ToString(), signatureFolder, "imzalar");
        Directory.CreateDirectory(folder);
        var fileName = $"{type}-{Guid.NewGuid():N}.png";
        var fullPath = Path.Combine(folder, fileName);
        await System.IO.File.WriteAllBytesAsync(fullPath, bytes);
        return ($"/uploads/tehlikeli-isler/{workId}/{signatureFolder}/imzalar/{fileName}", Sha256(bytes));
    }

    private void AddSignatureToExcel(XSSFWorkbook workbook, XSSFDrawing drawing, string? relativePath, int column, int rowFrom, int rowTo, int? columnTo = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(fullPath)) return;
        var bytes = System.IO.File.ReadAllBytes(fullPath);
        var pictureIndex = workbook.AddPicture(bytes, NPOI.SS.UserModel.PictureType.PNG);
        var anchor = new XSSFClientAnchor { Col1 = column, Row1 = rowFrom, Col2 = columnTo ?? column + 1, Row2 = rowTo };
        drawing.CreatePicture(anchor, pictureIndex);
    }

    private void ValidateFile(IFormFile file, bool signed)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length == 0 || !AllowedExtensions.Contains(ext) || file.Length > MaxFileSize)
            ModelState.AddModelError("", $"{file.FileName}: desteklenmeyen tür veya 50 MB üstü dosya.");
        if (signed && ext is not ".pdf" and not ".jpg" and not ".jpeg" and not ".png")
            ModelState.AddModelError("", "Islak imzalı belge PDF/JPG/PNG olmalıdır.");
    }

    private async Task AddAudit(int workId, int? dayId, int personId, string action, string description)
    {
        var previous = await _db.TehlikeliIsDenetimKayitlari.IgnoreQueryFilters().Where(x => x.TehlikeliIsId == workId).OrderByDescending(x => x.Id).Select(x => x.KayitOzeti).FirstOrDefaultAsync() ?? string.Empty;
        var now = DateTime.Now; var text = $"{workId}|{dayId}|{personId}|{action}|{description}|{now:O}|{previous}";
        _db.TehlikeliIsDenetimKayitlari.Add(new TehlikeliIsDenetimKaydi { TehlikeliIsId = workId, TehlikeliIsGunId = dayId, PersonelId = personId, Islem = action, Aciklama = description, IpAdresi = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers.UserAgent.ToString(), OncekiKayitOzeti = previous, KayitOzeti = Sha256(Encoding.UTF8.GetBytes(text)), CreatedDate = now, IsActive = true });
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Set(NPOI.SS.UserModel.ISheet sheet, string cellAddress, string? value)
    {
        var reference = new NPOI.SS.Util.CellReference(cellAddress);
        Set(sheet, reference.Row, reference.Col, value);
    }
    private static void Set(NPOI.SS.UserModel.ISheet sheet, int row, int col, string? value)
    {
        var targetRow = sheet.GetRow(row) ?? sheet.CreateRow(row);
        var targetCell = targetRow.GetCell(col) ?? targetRow.CreateCell(col);
        targetCell.SetCellValue(value ?? string.Empty);
    }
}
