using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class TehlikeSinifiController : Controller
{
    private readonly ApplicationDbContext _db;
    public TehlikeSinifiController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var query = _db.TehlikeSiniflari.AsNoTracking().Include(x => x.Maddeler);
        var paging = await PreparePaging(query, page, pageSize);
        ViewBag.Pagination = paging.Result;
        return View(await query.OrderBy(x => x.Sira).Skip(paging.Skip).Take(paging.Result.PageSize).ToListAsync());
    }
    [HttpGet] public IActionResult Ekle() => View("Form", new TehlikeSinifi { IsActive = true });
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Ekle(TehlikeSinifi model)
    {
        ModelState.Remove(nameof(model.Maddeler)); ModelState.Remove(nameof(model.TehlikeliIsler)); Normalize(model);
        if (await _db.TehlikeSiniflari.IgnoreQueryFilters().AnyAsync(x => !x.IsDeleted && x.Ad == model.Ad)) ModelState.AddModelError(nameof(model.Ad), "Bu tehlike sınıfı zaten mevcut.");
        if (!ModelState.IsValid) return View("Form", model);
        model.CreatedDate = DateTime.Now; _db.Add(model); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Duzenle(int id) { var x = await _db.TehlikeSiniflari.FindAsync(id); return x == null ? NotFound() : View("Form", x); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Duzenle(TehlikeSinifi model)
    {
        ModelState.Remove(nameof(model.Maddeler)); ModelState.Remove(nameof(model.TehlikeliIsler)); Normalize(model);
        var x = await _db.TehlikeSiniflari.FindAsync(model.Id); if (x == null) return NotFound();
        if (await _db.TehlikeSiniflari.AnyAsync(y => y.Id != model.Id && y.Ad == model.Ad)) ModelState.AddModelError(nameof(model.Ad), "Bu tehlike sınıfı zaten mevcut.");
        if (!ModelState.IsValid) return View("Form", model);
        x.Ad = model.Ad; x.Aciklama = model.Aciklama; x.Sira = model.Sira; x.IsActive = model.IsActive; x.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Sil(int id)
    {
        var x = await _db.TehlikeSiniflari.Include(x => x.Maddeler).FirstOrDefaultAsync(x => x.Id == id); if (x == null) return NotFound();
        if (await _db.TehlikeliIsler.AnyAsync(y => y.TehlikeSinifiId == id) || await _db.TehlikeliIsGunTehlikeSiniflari.AnyAsync(y => y.TehlikeSinifiId == id)) { TempData["ToastrError"] = "Kullanılan tehlike sınıfı silinemez; pasif yapabilirsiniz."; return RedirectToAction(nameof(Index)); }
        _db.RemoveRange(x.Maddeler); _db.Remove(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Maddeler(int id, int page = 1, int pageSize = 10)
    {
        ViewBag.Sinif = await _db.TehlikeSiniflari.FindAsync(id);
        if (ViewBag.Sinif == null) return NotFound();
        var query = _db.TehlikeSinifiMaddeleri.AsNoTracking().Where(x => x.TehlikeSinifiId == id);
        var paging = await PreparePaging(query, page, pageSize);
        ViewBag.Pagination = paging.Result;
        return View(await query.OrderBy(x => x.Sira).Skip(paging.Skip).Take(paging.Result.PageSize).ToListAsync());
    }
    [HttpGet] public async Task<IActionResult> MaddeEkle(int id) { ViewBag.Sinif = await _db.TehlikeSiniflari.FindAsync(id); return View("MaddeForm", new TehlikeSinifiMadde { TehlikeSinifiId = id, IsActive = true }); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MaddeEkle(TehlikeSinifiMadde model) { ModelState.Remove(nameof(model.TehlikeSinifi)); ModelState.Remove(nameof(model.GunMaddeleri)); if (!ModelState.IsValid) { ViewBag.Sinif = await _db.TehlikeSiniflari.FindAsync(model.TehlikeSinifiId); return View("MaddeForm", model); } model.Metin = model.Metin.Trim(); model.CreatedDate = DateTime.Now; _db.Add(model); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Maddeler), new { id = model.TehlikeSinifiId }); }
    [HttpGet] public async Task<IActionResult> MaddeDuzenle(int id) { var x = await _db.TehlikeSinifiMaddeleri.FindAsync(id); if (x == null) return NotFound(); ViewBag.Sinif = await _db.TehlikeSiniflari.FindAsync(x.TehlikeSinifiId); return View("MaddeForm", x); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MaddeDuzenle(TehlikeSinifiMadde model) { ModelState.Remove(nameof(model.TehlikeSinifi)); ModelState.Remove(nameof(model.GunMaddeleri)); var x = await _db.TehlikeSinifiMaddeleri.FindAsync(model.Id); if (x == null) return NotFound(); if (!ModelState.IsValid) { ViewBag.Sinif = await _db.TehlikeSiniflari.FindAsync(model.TehlikeSinifiId); return View("MaddeForm", model); } x.Metin = model.Metin.Trim(); x.Sira = model.Sira; x.ExcelSatirNo = model.ExcelSatirNo; x.IsActive = model.IsActive; x.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(); return RedirectToAction(nameof(Maddeler), new { id = x.TehlikeSinifiId }); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MaddeSil(int id) { var x = await _db.TehlikeSinifiMaddeleri.FindAsync(id); if (x == null) return NotFound(); var sid = x.TehlikeSinifiId; if (await _db.TehlikeliIsGunMaddeleri.AnyAsync(y => y.MaddeId == id)) { x.IsActive = false; x.UpdatedDate = DateTime.Now; } else _db.Remove(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Maddeler), new { id = sid }); }
    private static void Normalize(TehlikeSinifi x) { x.Ad = (x.Ad ?? "").Trim(); x.Aciklama = string.IsNullOrWhiteSpace(x.Aciklama) ? null : x.Aciklama.Trim(); }
    private static async Task<(PaginatedResult<object> Result, int Skip)> PreparePaging<T>(IQueryable<T> query, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100); page = Math.Max(page, 1);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        return (new PaginatedResult<object> { TotalCount = totalCount, PageNumber = page, PageSize = pageSize }, (page - 1) * pageSize);
    }
}
