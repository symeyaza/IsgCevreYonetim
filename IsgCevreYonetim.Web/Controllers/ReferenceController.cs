using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace IsgCevreYonetim.Web.Controllers
{
    public class ReferenceController : Controller
    {
        private readonly IReferenceService _referenceService;

        public ReferenceController(IReferenceService referenceService)
        {
            _referenceService = referenceService;
        }

        // =============================================
        // İL
        // =============================================
        public async Task<IActionResult> Iller(
            string? searchTerm = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = null,
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _referenceService.GetIllerAsync(filter);
            return View(result);
        }

        [HttpGet]
        public IActionResult IlEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlEkle(Il il)
        {
            if (await _referenceService.IlExistsAsync(il.Kod, il.Ad))
            {
                TempData["ToastrError"] = "Bu kod veya isimde bir il zaten mevcut!";
                return View(il);
            }

            await _referenceService.CreateIlAsync(il);
            TempData["ToastrSuccess"] = "İl başarıyla eklendi!";
            return RedirectToAction(nameof(Iller));
        }

        [HttpGet]
        public async Task<IActionResult> IlDuzenle(int id)
        {
            var il = await _referenceService.GetIlByIdAsync(id);
            if (il == null)
            {
                TempData["ToastrError"] = "İl bulunamadı!";
                return RedirectToAction(nameof(Iller));
            }
            return View(il);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlDuzenle(Il il)
        {
            await _referenceService.UpdateIlAsync(il);
            TempData["ToastrSuccess"] = "İl başarıyla güncellendi!";
            return RedirectToAction(nameof(Iller));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlSil(int id)
        {
            try
            {
                await _referenceService.DeleteIlAsync(id);
                TempData["ToastrSuccess"] = "İl başarıyla silindi!";
                return RedirectToAction(nameof(Iller));
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                return RedirectToAction(nameof(Iller));
            }
        }

        // =============================================
        // İLÇE
        // =============================================
        public async Task<IActionResult> Ilceler(
            int? ilId = null,
            string? searchTerm = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = null,
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _referenceService.GetIlcelerAsync(filter);
            ViewBag.IlId = ilId;
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> IlceEkle(int? ilId = null)
        {
            ViewBag.IlId = ilId;
            ViewBag.Iller = await _referenceService.GetAllIllerAsync();
            return View(new Ilce { IlId = ilId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlceEkle(Ilce ilce)
        {
            if (await _referenceService.IlceExistsAsync(ilce.IlId, ilce.Ad))
            {
                TempData["ToastrError"] = "Bu ilde aynı isimde bir ilçe zaten mevcut!";
                ViewBag.IlId = ilce.IlId;
                ViewBag.Iller = await _referenceService.GetAllIllerAsync();
                return View(ilce);
            }

            await _referenceService.CreateIlceAsync(ilce);
            TempData["ToastrSuccess"] = "İlçe başarıyla eklendi!";
            return RedirectToAction(nameof(Ilceler), new { ilId = ilce.IlId });
        }

        [HttpGet]
        public async Task<IActionResult> IlceDuzenle(int id)
        {
            var ilce = await _referenceService.GetIlceByIdAsync(id);
            if (ilce == null)
            {
                TempData["ToastrError"] = "İlçe bulunamadı!";
                return RedirectToAction(nameof(Ilceler));
            }
            ViewBag.IlId = ilce.IlId;
            ViewBag.Iller = await _referenceService.GetAllIllerAsync();
            return View(ilce);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlceDuzenle(Ilce ilce)
        {
            await _referenceService.UpdateIlceAsync(ilce);
            TempData["ToastrSuccess"] = "İlçe başarıyla güncellendi!";
            return RedirectToAction(nameof(Ilceler), new { ilId = ilce.IlId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlceSil(int id)
        {
            try
            {
                var ilce = await _referenceService.GetIlceByIdAsync(id);
                var ilId = ilce?.IlId ?? 0;
                await _referenceService.DeleteIlceAsync(id);
                TempData["ToastrSuccess"] = "İlçe başarıyla silindi!";
                return RedirectToAction(nameof(Ilceler), new { ilId });
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                var ilce = await _referenceService.GetIlceByIdAsync(id);
                return RedirectToAction(nameof(Ilceler), new { ilId = ilce?.IlId ?? 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIlcelerByIlId(int ilId)
        {
            var ilceler = await _referenceService.GetIlcelerByIlIdAsync(ilId);
            var result = ilceler.Select(i => new { id = i.Id, ad = i.Ad });
            return Json(result);
        }

        // =============================================
        // GÖREV
        // =============================================
        public async Task<IActionResult> Gorevler(
            string? searchTerm = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = null,
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _referenceService.GetGorevlerAsync(filter);
            return View(result);
        }

        [HttpGet]
        public IActionResult GorevEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GorevEkle(Gorev gorev)
        {
            if (await _referenceService.GorevExistsAsync(gorev.Ad))
            {
                TempData["ToastrError"] = "Bu isimde bir görev zaten mevcut!";
                return View(gorev);
            }

            await _referenceService.CreateGorevAsync(gorev);
            TempData["ToastrSuccess"] = "Görev başarıyla eklendi!";
            return RedirectToAction(nameof(Gorevler));
        }

        [HttpGet]
        public async Task<IActionResult> GorevDuzenle(int id)
        {
            var gorev = await _referenceService.GetGorevByIdAsync(id);
            if (gorev == null)
            {
                TempData["ToastrError"] = "Görev bulunamadı!";
                return RedirectToAction(nameof(Gorevler));
            }
            return View(gorev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GorevDuzenle(Gorev gorev)
        {
            await _referenceService.UpdateGorevAsync(gorev);
            TempData["ToastrSuccess"] = "Görev başarıyla güncellendi!";
            return RedirectToAction(nameof(Gorevler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GorevSil(int id)
        {
            try
            {
                await _referenceService.DeleteGorevAsync(id);
                TempData["ToastrSuccess"] = "Görev başarıyla silindi!";
                return RedirectToAction(nameof(Gorevler));
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                return RedirectToAction(nameof(Gorevler));
            }
        }

        // =============================================
        // GRUP
        // =============================================
        public async Task<IActionResult> Gruplar(
            string? searchTerm = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = null,
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _referenceService.GetGruplarAsync(filter);
            return View(result);
        }

        [HttpGet]
        public IActionResult GrupEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrupEkle(Grup grup)
        {
            if (await _referenceService.GrupExistsAsync(grup.Ad))
            {
                TempData["ToastrError"] = "Bu isimde bir grup zaten mevcut!";
                return View(grup);
            }

            await _referenceService.CreateGrupAsync(grup);
            TempData["ToastrSuccess"] = "Grup başarıyla eklendi!";
            return RedirectToAction(nameof(Gruplar));
        }

        [HttpGet]
        public async Task<IActionResult> GrupDuzenle(int id)
        {
            var grup = await _referenceService.GetGrupByIdAsync(id);
            if (grup == null)
            {
                TempData["ToastrError"] = "Grup bulunamadı!";
                return RedirectToAction(nameof(Gruplar));
            }
            return View(grup);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrupDuzenle(Grup grup)
        {
            await _referenceService.UpdateGrupAsync(grup);
            TempData["ToastrSuccess"] = "Grup başarıyla güncellendi!";
            return RedirectToAction(nameof(Gruplar));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrupSil(int id)
        {
            try
            {
                await _referenceService.DeleteGrupAsync(id);
                TempData["ToastrSuccess"] = "Grup başarıyla silindi!";
                return RedirectToAction(nameof(Gruplar));
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                return RedirectToAction(nameof(Gruplar));
            }
        }

        // =============================================
        // CİNSİYET
        // =============================================
        public async Task<IActionResult> Cinsiyetler(
            string? searchTerm = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = null,
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _referenceService.GetCinsiyetlerAsync(filter);
            return View(result);
        }

        [HttpGet]
        public IActionResult CinsiyetEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CinsiyetEkle(Cinsiyet cinsiyet)
        {
            if (await _referenceService.CinsiyetExistsAsync(cinsiyet.Ad))
            {
                TempData["ToastrError"] = "Bu isimde bir cinsiyet zaten mevcut!";
                return View(cinsiyet);
            }

            await _referenceService.CreateCinsiyetAsync(cinsiyet);
            TempData["ToastrSuccess"] = "Cinsiyet başarıyla eklendi!";
            return RedirectToAction(nameof(Cinsiyetler));
        }

        [HttpGet]
        public async Task<IActionResult> CinsiyetDuzenle(int id)
        {
            var cinsiyet = await _referenceService.GetCinsiyetByIdAsync(id);
            if (cinsiyet == null)
            {
                TempData["ToastrError"] = "Cinsiyet bulunamadı!";
                return RedirectToAction(nameof(Cinsiyetler));
            }
            return View(cinsiyet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CinsiyetDuzenle(Cinsiyet cinsiyet)
        {
            await _referenceService.UpdateCinsiyetAsync(cinsiyet);
            TempData["ToastrSuccess"] = "Cinsiyet başarıyla güncellendi!";
            return RedirectToAction(nameof(Cinsiyetler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CinsiyetSil(int id)
        {
            try
            {
                await _referenceService.DeleteCinsiyetAsync(id);
                TempData["ToastrSuccess"] = "Cinsiyet başarıyla silindi!";
                return RedirectToAction(nameof(Cinsiyetler));
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                return RedirectToAction(nameof(Cinsiyetler));
            }
        }
    }
}
