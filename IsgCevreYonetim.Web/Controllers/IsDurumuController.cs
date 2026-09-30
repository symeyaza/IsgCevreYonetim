using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class IsDurumuController : Controller
{
    private readonly ApplicationDbContext _db;
    public IsDurumuController(ApplicationDbContext db) => _db = db;
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var query = _db.IsDurumlari.AsNoTracking();
        pageSize = Math.Clamp(pageSize, 1, 100); page = Math.Max(page, 1);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        ViewBag.Pagination = new PaginatedResult<object> { TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
        return View(await query.OrderBy(x => x.Sira).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }
    [HttpGet] public IActionResult Ekle() => View("Form", new IsDurumu { IsActive = true });
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Ekle(IsDurumu model) { ModelState.Remove(nameof(model.TehlikeliIsler)); await Validate(model); if (!ModelState.IsValid) return View("Form", model); model.CreatedDate = DateTime.Now; _db.Add(model); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Duzenle(int id) { var x = await _db.IsDurumlari.FindAsync(id); return x == null ? NotFound() : View("Form", x); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Duzenle(IsDurumu model) { ModelState.Remove(nameof(model.TehlikeliIsler)); var x = await _db.IsDurumlari.FindAsync(model.Id); if (x == null) return NotFound(); await Validate(model); if (!ModelState.IsValid) return View("Form", model); x.Ad = model.Ad.Trim(); x.Renk = model.Renk; x.Sira = model.Sira; x.DevamKaydiAcabilir = model.DevamKaydiAcabilir; x.Tamamlandi = model.Tamamlandi; x.IsActive = model.IsActive; x.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Sil(int id) { var x = await _db.IsDurumlari.FindAsync(id); if (x == null) return NotFound(); if (await _db.TehlikeliIsler.AnyAsync(y => y.IsDurumuId == id)) { TempData["ToastrError"] = "Kullanılan iş durumu silinemez; pasif yapabilirsiniz."; return RedirectToAction(nameof(Index)); } _db.Remove(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    private async Task Validate(IsDurumu model) { model.Ad = (model.Ad ?? "").Trim(); if (await _db.IsDurumlari.IgnoreQueryFilters().AnyAsync(x => !x.IsDeleted && x.Id != model.Id && x.Ad == model.Ad)) ModelState.AddModelError(nameof(model.Ad), "Bu iş durumu zaten mevcut."); }
}
