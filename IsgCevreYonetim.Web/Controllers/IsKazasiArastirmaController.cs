using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Web.Services;
using IsgCevreYonetim.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace IsgCevreYonetim.Web.Controllers
{
    public class IsKazasiArastirmaController : Controller
    {
        private readonly IIsKazasiArastirmaService _service;
        private readonly IUserScopeService _userScopeService;

        public IsKazasiArastirmaController(
            IIsKazasiArastirmaService service,
            IUserScopeService userScopeService)
        {
            _service = service;
            _userScopeService = userScopeService;
        }

        public async Task<IActionResult> Kategoriler()
        {
            return View(await _service.GetKategorilerAsync());
        }

        [HttpGet]
        public IActionResult KategoriEkle() => View(new IsKazasiArastirmaKategori { IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KategoriEkle(IsKazasiArastirmaKategori model)
        {
            ModelState.Remove(nameof(model.Maddeler));
            if (!ModelState.IsValid) return View(model);

            try
            {
                await _service.CreateKategoriAsync(model);
                TempData["ToastrSuccess"] = "Araştırma kategorisi başarıyla eklendi.";
                return RedirectToAction(nameof(Kategoriler));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> KategoriDuzenle(int id)
        {
            var model = await _service.GetKategoriByIdAsync(id);
            if (model == null)
            {
                TempData["ToastrError"] = "Araştırma kategorisi bulunamadı.";
                return RedirectToAction(nameof(Kategoriler));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KategoriDuzenle(IsKazasiArastirmaKategori model)
        {
            ModelState.Remove(nameof(model.Maddeler));
            if (!ModelState.IsValid) return View(model);

            try
            {
                await _service.UpdateKategoriAsync(model);
                TempData["ToastrSuccess"] = "Araştırma kategorisi güncellendi.";
                return RedirectToAction(nameof(Kategoriler));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KategoriSil(int id)
        {
            try
            {
                await _service.DeleteKategoriAsync(id);
                TempData["ToastrSuccess"] = "Araştırma kategorisi silindi.";
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
            }
            return RedirectToAction(nameof(Kategoriler));
        }

        public async Task<IActionResult> Maddeler(
            int? kategoriId = null,
            int page = 1,
            int pageSize = 10)
        {
            ViewBag.KategoriId = kategoriId;
            ViewBag.Kategoriler = await _service.GetKategorilerAsync();
            return View(await _service.GetMaddelerPagedAsync(
                kategoriId,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)));
        }

        [HttpGet]
        public async Task<IActionResult> MaddeEkle(int? kategoriId = null)
        {
            ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
            return View(new IsKazasiArastirmaMadde
            {
                KategoriId = kategoriId ?? 0,
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MaddeEkle(IsKazasiArastirmaMadde model)
        {
            ModelState.Remove(nameof(model.Kategori));
            ModelState.Remove(nameof(model.Cevaplar));
            if (!ModelState.IsValid)
            {
                ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
                return View(model);
            }

            try
            {
                await _service.CreateMaddeAsync(model);
                TempData["ToastrSuccess"] = "Araştırma maddesi başarıyla eklendi.";
                return RedirectToAction(nameof(Maddeler), new { kategoriId = model.KategoriId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> MaddeDuzenle(int id)
        {
            var model = await _service.GetMaddeByIdAsync(id);
            if (model == null)
            {
                TempData["ToastrError"] = "Araştırma maddesi bulunamadı.";
                return RedirectToAction(nameof(Maddeler));
            }
            ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MaddeDuzenle(IsKazasiArastirmaMadde model)
        {
            ModelState.Remove(nameof(model.Kategori));
            ModelState.Remove(nameof(model.Cevaplar));
            if (!ModelState.IsValid)
            {
                ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
                return View(model);
            }

            try
            {
                await _service.UpdateMaddeAsync(model);
                TempData["ToastrSuccess"] = "Araştırma maddesi güncellendi.";
                return RedirectToAction(nameof(Maddeler), new { kategoriId = model.KategoriId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Kategoriler = await _service.GetKategorilerAsync(false);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MaddeSil(int id)
        {
            var item = await _service.GetMaddeByIdAsync(id);
            try
            {
                await _service.DeleteMaddeAsync(id);
                TempData["ToastrSuccess"] = "Araştırma maddesi silindi.";
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
            }
            return RedirectToAction(nameof(Maddeler), new { kategoriId = item?.KategoriId });
        }

        [HttpGet]
        public async Task<IActionResult> Arastirma(int id)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var accident = await _service.GetIsKazasiAsync(id, scope.ActiveBranchId);
            if (accident == null)
            {
                TempData["ToastrError"] = "İş kazası bulunamadı veya aktif şubeye ait değil.";
                return RedirectToAction("Index", "IsKazasi");
            }

            var model = new IsKazasiArastirmaViewModel
            {
                IsKazasi = accident,
                Kategoriler = await _service.GetAktifKategorilerVeMaddelerAsync(),
                SeciliMaddeIdleri = await _service.GetSeciliMaddeIdleriAsync(id, scope.ActiveBranchId),
                Arastirma = await _service.GetArastirmaBilgisiAsync(id, scope.ActiveBranchId)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArastirmaGuncelle(int id, List<int>? seciliMaddeIdleri)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            try
            {
                await _service.SaveArastirmaAsync(
                    id,
                    scope.ActiveBranchId,
                    scope.PersonelId,
                    seciliMaddeIdleri);

                TempData["ToastrSuccess"] = "Ayrıntılı kaza araştırması kaydedildi.";
                return RedirectToAction(nameof(Arastirma), new { id });
            }
            catch (Exception ex)
            {
                TempData["ToastrError"] = ex.Message;
                return RedirectToAction(nameof(Arastirma), new { id });
            }
        }
    }
}
