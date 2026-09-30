using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers
{
    [Authorize]
    public class DashboardGuncellemeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardGuncellemeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DashboardGuncellemeKategori? kategori,
            DashboardGuncellemeDurum? durum,
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.DashboardGuncellemeleri
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (kategori.HasValue)
                query = query.Where(x => x.Kategori == kategori.Value);

            if (durum.HasValue)
                query = query.Where(x => x.Durum == durum.Value);

            ViewBag.Kategori = kategori;
            ViewBag.Durum = durum;

            var orderedQuery = query
                .OrderBy(x => x.Kategori)
                .ThenBy(x => x.Durum)
                .ThenBy(x => x.Sira)
                .ThenBy(x => x.SayfaAdi);

            var totalCount = await orderedQuery.CountAsync();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
            page = Math.Min(page, totalPages);

            var records = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(new PaginatedResult<DashboardGuncelleme>
            {
                Items = records,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            });
        }

        [HttpGet]
        public IActionResult Ekle()
        {
            return View(new DashboardGuncelleme
            {
                Kategori = DashboardGuncellemeKategori.Genel,
                Durum = DashboardGuncellemeDurum.Planlanan,
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(DashboardGuncelleme model)
        {
            NormalizeAndValidate(model);
            if (!ModelState.IsValid)
                return View(model);

            model.CreatedDate = DateTime.Now;
            model.UpdatedDate = null;
            model.IsDeleted = false;

            _context.DashboardGuncellemeleri.Add(model);
            await _context.SaveChangesAsync();

            TempData["ToastrSuccess"] = $"{model.SayfaAdi} güncelleme listesine eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var record = await _context.DashboardGuncellemeleri
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (record == null)
                return NotFound();

            return View(record);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(DashboardGuncelleme model)
        {
            var record = await _context.DashboardGuncellemeleri
                .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted);

            if (record == null)
                return NotFound();

            NormalizeAndValidate(model);
            if (!ModelState.IsValid)
                return View(model);

            record.SayfaAdi = model.SayfaAdi;
            record.Kategori = model.Kategori;
            record.Durum = model.Durum;
            record.Aciklama = model.Aciklama;
            record.Sira = model.Sira;
            record.IsActive = model.IsActive;
            record.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["ToastrSuccess"] = $"{record.SayfaAdi} güncellemesi güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var record = await _context.DashboardGuncellemeleri
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (record == null)
            {
                TempData["ToastrError"] = "Silinecek güncelleme kaydı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            _context.DashboardGuncellemeleri.Remove(record);
            await _context.SaveChangesAsync();

            TempData["ToastrSuccess"] = $"{record.SayfaAdi} güncellemesi silindi.";
            return RedirectToAction(nameof(Index));
        }

        private void NormalizeAndValidate(DashboardGuncelleme model)
        {
            model.SayfaAdi = (model.SayfaAdi ?? string.Empty).Trim();
            model.Aciklama = string.IsNullOrWhiteSpace(model.Aciklama)
                ? null
                : model.Aciklama.Trim();

            if (!Enum.IsDefined(model.Kategori))
                ModelState.AddModelError(nameof(model.Kategori), "Geçerli bir kategori seçiniz.");

            if (!Enum.IsDefined(model.Durum))
                ModelState.AddModelError(nameof(model.Durum), "Geçerli bir durum seçiniz.");
        }
    }
}
