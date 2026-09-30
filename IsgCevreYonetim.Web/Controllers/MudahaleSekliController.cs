using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers
{
    public class MudahaleSekliController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MudahaleSekliController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(string searchTerm = "")
        {
            var query = _db.MudahaleSekilleri.IgnoreQueryFilters().AsNoTracking()
                .Where(x => !x.IsDeleted);

            searchTerm = (searchTerm ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(x => x.Ad.Contains(searchTerm) ||
                    (x.Aciklama != null && x.Aciklama.Contains(searchTerm)));

            ViewBag.SearchTerm = searchTerm;
            return View(await query.OrderBy(x => x.Sira).ThenBy(x => x.Ad).ToListAsync());
        }

        [HttpGet]
        public IActionResult Ekle() => View(new MudahaleSekli());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(MudahaleSekli model)
        {
            Normalize(model);
            await ValidateUniqueAsync(model);
            if (!ModelState.IsValid) return View(model);

            model.CreatedDate = DateTime.Now;
            model.IsActive = true;
            model.IsDeleted = false;
            _db.MudahaleSekilleri.Add(model);
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Müdahale şekli başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var model = await _db.MudahaleSekilleri.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(MudahaleSekli model)
        {
            var entity = await _db.MudahaleSekilleri.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted);
            if (entity == null) return NotFound();

            Normalize(model);
            await ValidateUniqueAsync(model);
            if (!ModelState.IsValid) return View(model);

            entity.Ad = model.Ad;
            entity.Aciklama = model.Aciklama;
            entity.Sira = model.Sira;
            entity.IsActive = model.IsActive;
            entity.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Müdahale şekli başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var entity = await _db.MudahaleSekilleri.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (entity == null)
            {
                TempData["ToastrError"] = "Müdahale şekli bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var kullaniliyor = await _db.IsKazalari.IgnoreQueryFilters()
                .AnyAsync(x => x.MudahaleSekliId == id);
            if (kullaniliyor)
            {
                TempData["ToastrError"] = "Bu müdahale şekli iş kazalarında kullanıldığı için silinemez. Pasif duruma getirebilirsiniz.";
                return RedirectToAction(nameof(Index));
            }

            _db.MudahaleSekilleri.Remove(entity);
            await _db.SaveChangesAsync();
            TempData["ToastrSuccess"] = "Müdahale şekli başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        private static void Normalize(MudahaleSekli model)
        {
            model.Ad = (model.Ad ?? string.Empty).Trim();
            model.Aciklama = string.IsNullOrWhiteSpace(model.Aciklama) ? null : model.Aciklama.Trim();
            if (model.Sira < 0) model.Sira = 0;
        }

        private async Task ValidateUniqueAsync(MudahaleSekli model)
        {
            if (string.IsNullOrWhiteSpace(model.Ad))
                ModelState.AddModelError(nameof(model.Ad), "Müdahale şekli adı zorunludur.");

            var exists = await _db.MudahaleSekilleri.IgnoreQueryFilters()
                .AnyAsync(x => !x.IsDeleted && x.Id != model.Id && x.Ad == model.Ad);
            if (exists)
                ModelState.AddModelError(nameof(model.Ad), "Bu müdahale şekli zaten kayıtlı.");
        }
    }
}
