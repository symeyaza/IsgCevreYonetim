using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers;

public class IsKazasiDuzelticiFaaliyetController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUserScopeService _scopeService;
    private readonly IWebHostEnvironment _env;
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf", ".mp4", ".webm", ".mov" };

    public IsKazasiDuzelticiFaaliyetController(ApplicationDbContext db, IUserScopeService scopeService, IWebHostEnvironment env)
    { _db = db; _scopeService = scopeService; _env = env; }

    private async Task<IsKazasi?> GetKaza(int id)
    {
        var scope = await _scopeService.GetAsync();
        if (scope == null) return null;
        return await _db.IsKazalari.AsNoTracking().Include(x => x.Personel)
            .FirstOrDefaultAsync(x => x.Id == id && x.BranchId == scope.ActiveBranchId);
    }

    public async Task<IActionResult> Index(int isKazasiId)
    {
        var kaza = await GetKaza(isKazasiId); if (kaza == null) return NotFound();
        ViewBag.Kaza = kaza;
        var list = await _db.IsKazasiDuzelticiFaaliyetler.AsNoTracking().Include(x => x.Dosyalar)
            .Where(x => x.IsKazasiId == isKazasiId).OrderByDescending(x => x.CreatedDate).ToListAsync();
        return View(list);
    }

    [HttpGet] public async Task<IActionResult> Ekle(int isKazasiId)
    { var kaza = await GetKaza(isKazasiId); if (kaza == null) return NotFound(); ViewBag.Kaza = kaza; return View(new IsKazasiDuzelticiFaaliyet { IsKazasiId = isKazasiId }); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(IsKazasiDuzelticiFaaliyet model, List<IFormFile>? dosyalar)
    {
        var kaza = await GetKaza(model.IsKazasiId); if (kaza == null) return NotFound();
        // Navigation/collection alanlari formdan POST edilmez. Entity dogrudan bind edildigi icin
        // ASP.NET Core bunlari implicit required kabul edip ModelState'i gecersiz yapabilir.
        ModelState.Remove(nameof(IsKazasiDuzelticiFaaliyet.IsKazasi));
        ModelState.Remove(nameof(IsKazasiDuzelticiFaaliyet.Dosyalar));
        if (!ModelState.IsValid)
        {
            ViewBag.Kaza = kaza;
            return View(model);
        }
        model.CreatedDate = DateTime.Now; model.IsActive = true; model.IsDeleted = false;
        _db.IsKazasiDuzelticiFaaliyetler.Add(model); await _db.SaveChangesAsync();
        await SaveFiles(model.Id, dosyalar); TempData["ToastrSuccess"] = "Düzeltici faaliyet eklendi.";
        return RedirectToAction(nameof(Index), new { isKazasiId = model.IsKazasiId });
    }

    [HttpGet] public async Task<IActionResult> Duzenle(int id)
    {
        var item = await _db.IsKazasiDuzelticiFaaliyetler.AsNoTracking().Include(x => x.IsKazasi).FirstOrDefaultAsync(x => x.Id == id);
        if (item == null || await GetKaza(item.IsKazasiId) == null) return NotFound(); ViewBag.Kaza = item.IsKazasi; return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(IsKazasiDuzelticiFaaliyet model, List<IFormFile>? dosyalar)
    {
        var kaza = await GetKaza(model.IsKazasiId); if (kaza == null) return NotFound();
        var item = await _db.IsKazasiDuzelticiFaaliyetler.FirstOrDefaultAsync(x => x.Id == model.Id && x.IsKazasiId == model.IsKazasiId); if (item == null) return NotFound();
        ModelState.Remove(nameof(IsKazasiDuzelticiFaaliyet.IsKazasi));
        ModelState.Remove(nameof(IsKazasiDuzelticiFaaliyet.Dosyalar));
        if (!ModelState.IsValid) { ViewBag.Kaza = kaza; return View(model); }
        item.Baslik = model.Baslik; item.Aciklama = model.Aciklama; item.TamamlanmaTarihi = model.TamamlanmaTarihi; item.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(); await SaveFiles(item.Id, dosyalar); TempData["ToastrSuccess"] = "Düzeltici faaliyet güncellendi.";
        return RedirectToAction(nameof(Index), new { isKazasiId = item.IsKazasiId });
    }

    public async Task<IActionResult> Detay(int id)
    {
        var item = await _db.IsKazasiDuzelticiFaaliyetler.AsNoTracking().Include(x => x.Dosyalar).Include(x => x.IsKazasi).ThenInclude(x => x.Personel).FirstOrDefaultAsync(x => x.Id == id);
        if (item == null || await GetKaza(item.IsKazasiId) == null) return NotFound(); return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var item = await _db.IsKazasiDuzelticiFaaliyetler.Include(x => x.Dosyalar).FirstOrDefaultAsync(x => x.Id == id); if (item == null || await GetKaza(item.IsKazasiId) == null) return NotFound();
        foreach (var f in item.Dosyalar) DeletePhysical(f.DosyaYolu);
        var kazaId = item.IsKazasiId; _db.IsKazasiDuzelticiFaaliyetler.Remove(item); await _db.SaveChangesAsync(); TempData["ToastrSuccess"] = "Düzeltici faaliyet silindi.";
        return RedirectToAction(nameof(Index), new { isKazasiId = kazaId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DosyaSil(int id)
    {
        var f = await _db.IsKazasiDuzelticiFaaliyetDosyalari.Include(x => x.DuzelticiFaaliyet).FirstOrDefaultAsync(x => x.Id == id); if (f == null || await GetKaza(f.DuzelticiFaaliyet.IsKazasiId) == null) return NotFound();
        var faaliyetId=f.IsKazasiDuzelticiFaaliyetId; DeletePhysical(f.DosyaYolu); _db.Remove(f); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Detay), new { id = faaliyetId });
    }

    private async Task SaveFiles(int faaliyetId, List<IFormFile>? files)
    {
        if (files == null) return;
        var dir = Path.Combine(_env.WebRootPath, "uploads", "duzeltici-faaliyetler", faaliyetId.ToString()); Directory.CreateDirectory(dir);
        foreach (var file in files.Where(x => x.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName); if (!Allowed.Contains(ext)) continue;
            var safe = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}"; var path = Path.Combine(dir, safe);
            await using (var fs = System.IO.File.Create(path)) await file.CopyToAsync(fs);
            var type = file.ContentType.StartsWith("image/") ? "image" : file.ContentType.StartsWith("video/") ? "video" : "pdf";
            _db.IsKazasiDuzelticiFaaliyetDosyalari.Add(new() { IsKazasiDuzelticiFaaliyetId=faaliyetId, DosyaAdi=Path.GetFileName(file.FileName), DosyaYolu=$"/uploads/duzeltici-faaliyetler/{faaliyetId}/{safe}", DosyaTipi=type, DosyaBoyutu=file.Length, CreatedDate=DateTime.Now });
        }
        await _db.SaveChangesAsync();
    }
    private void DeletePhysical(string? url) { if (string.IsNullOrWhiteSpace(url)) return; var p=Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)); if (System.IO.File.Exists(p)) try { System.IO.File.Delete(p); } catch { } }
}
