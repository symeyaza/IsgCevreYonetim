using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers
{
    public class VardiyaController : Controller
    {
        private readonly ApplicationDbContext _db;

        public VardiyaController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vardiyalar = await _db.Vardiyalar
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(v => !v.IsDeleted)
                .OrderBy(v => v.VardiyaId)
                .ToListAsync();
            return View(vardiyalar);
        }

        [HttpGet]
        public IActionResult Ekle() => View(new Vardiya());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(Vardiya vardiya)
        {
            vardiya.VardiyaAdi = (vardiya.VardiyaAdi ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(vardiya.VardiyaAdi))
                ModelState.AddModelError(nameof(Vardiya.VardiyaAdi), "Vardiya adı zorunludur.");

            var exists = await _db.Vardiyalar.IgnoreQueryFilters()
                .AnyAsync(v => !v.IsDeleted && v.VardiyaAdi == vardiya.VardiyaAdi);
            if (exists)
                ModelState.AddModelError(nameof(Vardiya.VardiyaAdi), "Bu vardiya zaten kayıtlı.");

            if (!ModelState.IsValid) return View(vardiya);

            vardiya.CreatedDate = DateTime.Now;
            vardiya.UpdatedDate = null;
            vardiya.IsActive = true;
            vardiya.IsDeleted = false;
            _db.Vardiyalar.Add(vardiya);
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Vardiya başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var vardiya = await _db.Vardiyalar.IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.VardiyaId == id && !v.IsDeleted);
            if (vardiya == null) return NotFound();
            return View(vardiya);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(Vardiya model)
        {
            var vardiya = await _db.Vardiyalar.IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.VardiyaId == model.VardiyaId && !v.IsDeleted);
            if (vardiya == null) return NotFound();

            model.VardiyaAdi = (model.VardiyaAdi ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(model.VardiyaAdi))
                ModelState.AddModelError(nameof(Vardiya.VardiyaAdi), "Vardiya adı zorunludur.");

            var exists = await _db.Vardiyalar.IgnoreQueryFilters().AnyAsync(v =>
                !v.IsDeleted && v.VardiyaId != model.VardiyaId && v.VardiyaAdi == model.VardiyaAdi);
            if (exists)
                ModelState.AddModelError(nameof(Vardiya.VardiyaAdi), "Bu vardiya zaten kayıtlı.");

            if (!ModelState.IsValid) return View(model);

            vardiya.VardiyaAdi = model.VardiyaAdi;
            vardiya.IsActive = model.IsActive;
            vardiya.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Vardiya başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var vardiya = await _db.Vardiyalar.IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.VardiyaId == id && !v.IsDeleted);
            if (vardiya == null)
            {
                TempData["ToastrError"] = "Vardiya bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var kullaniliyor = await _db.IsKazalari.IgnoreQueryFilters()
                .AnyAsync(k => k.VardiyaId == id);
            if (kullaniliyor)
            {
                TempData["ToastrError"] = "Bu vardiya iş kazası kayıtlarında kullanıldığı için silinemez. Pasif duruma getirebilirsiniz.";
                return RedirectToAction(nameof(Index));
            }

            _db.Vardiyalar.Remove(vardiya);
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Vardiya başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
