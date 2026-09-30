using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class TaseronController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUserScopeService _scope;
    public TaseronController(ApplicationDbContext db, IUserScopeService scope) { _db = db; _scope = scope; }

    public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var q = _db.TaseronFirmalar.AsNoTracking().Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        if (!string.IsNullOrWhiteSpace(searchTerm)) q = q.Where(x => x.FirmaAdi.Contains(searchTerm.Trim()));
        pageSize = pageSize is 5 or 10 or 25 or 50 or 100 ? pageSize : 10;
        var total = await q.CountAsync(); page = Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
        ViewBag.Pagination = new IsgCevreYonetim.Shared.DTOs.PaginatedResult<object> { TotalCount = total, PageSize = pageSize, PageNumber = page };
        ViewBag.SearchTerm = searchTerm;
        return View(await q.OrderBy(x => x.FirmaAdi).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }
    [HttpGet] public IActionResult Ekle() => View("Form", new TaseronFirma());
    [HttpGet] public async Task<IActionResult> Duzenle(int id)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = await _db.TaseronFirmalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        return row == null ? NotFound() : View("Form", row);
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Ekle(TaseronFirma model) => SaveFirm(model, false);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Duzenle(TaseronFirma model) => SaveFirm(model, true);
    private async Task<IActionResult> SaveFirm(TaseronFirma model, bool edit)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = edit ? await _db.TaseronFirmalar.FirstOrDefaultAsync(x => x.Id == model.Id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId) : null;
        if (edit && row == null) return NotFound();
        ModelState.Remove(nameof(model.Kisiler));
        model.FirmaAdi = (model.FirmaAdi ?? "").Trim();
        if (string.IsNullOrWhiteSpace(model.FirmaAdi)) ModelState.AddModelError(nameof(model.FirmaAdi), "Firma adı zorunludur.");
        if (!ModelState.IsValid) return View("Form", model);
        row ??= new TaseronFirma { CompanyId = scope.ActiveCompanyId, BranchId = scope.ActiveBranchId, CreatedDate = DateTime.Now };
        row.FirmaAdi = model.FirmaAdi; row.VergiNo = model.VergiNo?.Trim(); row.Telefon = model.Telefon?.Trim();
        if (edit) row.UpdatedDate = DateTime.Now; else _db.TaseronFirmalar.Add(row);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { ModelState.AddModelError(nameof(model.FirmaAdi), "Bu şubede aynı isimli firma zaten kayıtlı."); return View("Form", model); }
        return RedirectToAction(nameof(Detay), new { id = row.Id });
    }
    [HttpGet] public async Task<IActionResult> Detay(int id)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = await _db.TaseronFirmalar.AsNoTracking().Include(x => x.Kisiler)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        return row == null ? NotFound() : View(row);
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Sil(int id)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = await _db.TaseronFirmalar.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        if (row == null) return NotFound();
        if (await _db.TehlikeliIsler.AnyAsync(x => x.TaseronFirmaId == id) || await _db.TaseronKisiler.AnyAsync(x => x.TaseronFirmaId == id))
        { TempData["ToastrError"] = "Firmaya bağlı kişi veya tehlikeli iş kaydı varken silinemez."; return RedirectToAction(nameof(Detay), new { id }); }
        row.IsDeleted = true; row.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> KisiEkle(int firmaId) => await PersonForm(new TaseronKisi { TaseronFirmaId = firmaId });
    [HttpGet] public async Task<IActionResult> KisiDuzenle(int id)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = await _db.TaseronKisiler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        return row == null ? NotFound() : await PersonForm(row);
    }
    private async Task<IActionResult> PersonForm(TaseronKisi model)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        ViewBag.Firmalar = await _db.TaseronFirmalar.AsNoTracking().Where(x => x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId).OrderBy(x => x.FirmaAdi).ToListAsync();
        return View("KisiForm", model);
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> KisiEkle(TaseronKisi model) => SavePerson(model, false);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> KisiDuzenle(TaseronKisi model) => SavePerson(model, true);
    private async Task<IActionResult> SavePerson(TaseronKisi model, bool edit)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = edit ? await _db.TaseronKisiler.FirstOrDefaultAsync(x => x.Id == model.Id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId) : null;
        if (edit && row == null) return NotFound();
        var firmId = edit ? row!.TaseronFirmaId : model.TaseronFirmaId;
        if (!await _db.TaseronFirmalar.AnyAsync(x => x.Id == firmId && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId)) ModelState.AddModelError(nameof(model.TaseronFirmaId), "Aktif şubeden firma seçin.");
        if (!model.YetkiliMi && !model.CalisanMi) ModelState.AddModelError("", "Kişi yetkili veya çalışan olarak işaretlenmelidir.");
        model.AdSoyad = (model.AdSoyad ?? "").Trim();
        if (string.IsNullOrWhiteSpace(model.AdSoyad)) ModelState.AddModelError(nameof(model.AdSoyad), "Ad soyad zorunludur.");
        ModelState.Remove(nameof(model.TaseronFirma));
        if (!ModelState.IsValid) return await PersonForm(model);
        row ??= new TaseronKisi { CompanyId = scope.ActiveCompanyId, BranchId = scope.ActiveBranchId, TaseronFirmaId = firmId, CreatedDate = DateTime.Now };
        row.AdSoyad = model.AdSoyad; row.KimlikNo = model.KimlikNo?.Trim(); row.Telefon = model.Telefon?.Trim(); row.YetkiliMi = model.YetkiliMi; row.CalisanMi = model.CalisanMi;
        if (edit) row.UpdatedDate = DateTime.Now; else _db.TaseronKisiler.Add(row);
        await _db.SaveChangesAsync(); return RedirectToAction(nameof(Detay), new { id = firmId });
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> KisiSil(int id)
    {
        var scope = await _scope.GetAsync(); if (scope == null) return Forbid();
        var row = await _db.TaseronKisiler.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == scope.ActiveCompanyId && x.BranchId == scope.ActiveBranchId);
        if (row == null) return NotFound();
        if (await _db.TehlikeliIsler.AnyAsync(x => x.TaseronYetkiliId == id) || await _db.TehlikeliIsKisileri.AnyAsync(x => x.TaseronKisiId == id))
        { TempData["ToastrError"] = "Tehlikeli iş kaydında kullanılan kişi silinemez."; return RedirectToAction(nameof(Detay), new { id = row.TaseronFirmaId }); }
        row.IsDeleted = true; row.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(); return RedirectToAction(nameof(Detay), new { id = row.TaseronFirmaId });
    }
}
