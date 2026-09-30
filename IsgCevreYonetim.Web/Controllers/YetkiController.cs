using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IsgCevreYonetim.Web.Controllers
{
    public class YetkiController : Controller
    {
        private readonly IYetkiService _yetkiService;
        private readonly IOrganizationService _organizationService;
        private readonly IUserScopeService _userScopeService;
        private readonly IPagePermissionService _pagePermissionService;

        public YetkiController(
            IYetkiService yetkiService,
            IOrganizationService organizationService,
            IUserScopeService userScopeService,
            IPagePermissionService pagePermissionService)
        {
            _yetkiService = yetkiService;
            _organizationService = organizationService;
            _userScopeService = userScopeService;
            _pagePermissionService = pagePermissionService;
        }

        // =============================================
        // YETKİLENDİRME ANA SAYFA
        // =============================================
        public async Task<IActionResult> Index()
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var yetkiler = await _yetkiService.GetAllYetkilerAsync();
            var sayfalar = await _yetkiService.GetAllSayfalarAsync();
            var lokasyonlar = scope.IsSystemAdmin
                ? await _organizationService.GetAllBranchesAsync()
                : scope.CanSelectBranch
                    ? await _organizationService.GetBranchesByCompanyIdAsync(scope.CompanyId)
                    : new[] { await _organizationService.GetBranchByIdAsync(scope.ActiveBranchId) }
                    .Where(x => x != null)
                    .Cast<Branch>()
                    .ToList();

            ViewBag.Yetkiler = yetkiler;
            ViewBag.Sayfalar = sayfalar;
            ViewBag.Lokasyonlar = lokasyonlar;

            return View();
        }

        // =============================================
        // YETKİ CRUD
        // =============================================
        public async Task<IActionResult> Yetkiler(
            int page = 1,
            int pageSize = 10,
            string searchTerm = "",
            string sortBy = "ad",
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _yetkiService.GetYetkilerAsync(filter);

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDescending = sortDescending;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            ViewBag.TotalCount = result.TotalCount;

            return View(result.Items);
        }

        [HttpGet]
        public IActionResult YetkiEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiEkle(Yetki yetki)
        {
            if (await _yetkiService.YetkiExistsAsync(yetki.Ad))
            {
                TempData["ToastrError"] = "Bu isimde bir yetki zaten mevcut!";
                return View(yetki);
            }

            await _yetkiService.CreateYetkiAsync(yetki);
            _pagePermissionService.InvalidateAll();
            TempData["ToastrSuccess"] = "Yetki başarıyla eklendi!";
            return RedirectToAction(nameof(Yetkiler));
        }

        [HttpGet]
        public async Task<IActionResult> YetkiDuzenle(int id)
        {
            var yetki = await _yetkiService.GetYetkiByIdAsync(id);

            if (yetki == null)
            {
                TempData["ToastrError"] = "Yetki bulunamadı!";
                return RedirectToAction(nameof(Yetkiler));
            }

            return View(yetki);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiDuzenle(Yetki yetki)
        {
            await _yetkiService.UpdateYetkiAsync(yetki);
            _pagePermissionService.InvalidateAll();
            TempData["ToastrSuccess"] = "Yetki başarıyla güncellendi!";
            return RedirectToAction(nameof(Yetkiler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiSil(int id)
        {
            try
            {
                await _yetkiService.DeleteYetkiAsync(id);
                _pagePermissionService.InvalidateAll();
                return Json(new { success = true, message = "Yetki başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // SAYFA CRUD
        // =============================================
        public async Task<IActionResult> Sayfalar(
            int page = 1,
            int pageSize = 10,
            string searchTerm = "",
            string sortBy = "ad",
            bool sortDescending = false)
        {
            var filter = new ReferenceFilterDto
            {
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _yetkiService.GetSayfalarAsync(filter);

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDescending = sortDescending;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            ViewBag.TotalCount = result.TotalCount;

            return View(result.Items);
        }

        [HttpGet]
        public async Task<IActionResult> SayfaEkle()
        {
            ViewBag.Sayfalar = await _yetkiService.GetAllSayfalarAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SayfaEkle(Sayfa sayfa)
        {
            await _yetkiService.CreateSayfaAsync(sayfa);
            _pagePermissionService.InvalidateAll();
            TempData["ToastrSuccess"] = "Sayfa başarıyla eklendi!";
            return RedirectToAction(nameof(Sayfalar));
        }

        [HttpGet]
        public async Task<IActionResult> SayfaDuzenle(int id)
        {
            var sayfa = await _yetkiService.GetSayfaByIdAsync(id);

            if (sayfa == null)
            {
                TempData["ToastrError"] = "Sayfa bulunamadı!";
                return RedirectToAction(nameof(Sayfalar));
            }

            ViewBag.Sayfalar = await _yetkiService.GetAllSayfalarAsync();
            return View(sayfa);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SayfaDuzenle(Sayfa sayfa)
        {
            await _yetkiService.UpdateSayfaAsync(sayfa);
            _pagePermissionService.InvalidateAll();
            TempData["ToastrSuccess"] = "Sayfa başarıyla güncellendi!";
            return RedirectToAction(nameof(Sayfalar));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SayfaSil(int id)
        {
            try
            {
                await _yetkiService.DeleteSayfaAsync(id);
                _pagePermissionService.InvalidateAll();
                return Json(new { success = true, message = "Sayfa başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // AJAX - YETKİLENDİRME
        // =============================================
        [HttpGet]
        public async Task<IActionResult> GetSayfalar()
        {
            var sayfalar = await _yetkiService.GetAllSayfalarAsync();

            return Json(sayfalar.Select(s => new
            {
                id = s.Id,
                ad = s.Ad,
                url = s.Url,
                icon = s.Icon ?? "fa-file",
                parentId = s.ParentId
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetYetkiSayfalar(int yetkiId, int branchId)
        {
            if (!await CanManageTargetBranchAsync(branchId))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Seçilen şube kendi şirketinizin kapsamında değil."
                });
            }

            var yetkiSayfalar =
                await _yetkiService.GetYetkiSayfalarByYetkiAndBranchIdAsync(
                    yetkiId,
                    branchId);

            var result = yetkiSayfalar.Select(ys => new
            {
                sayfaId = ys.SayfaId,
                ekle = ys.Ekle,
                guncelle = ys.Guncelle,
                sil = ys.Sil,
                goster = ys.Goster
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetYetkiSubeAyari(
            int yetkiId,
            int branchId)
        {
            if (!await CanManageTargetBranchAsync(branchId))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Seçilen şube kendi şirketinizin kapsamında değil."
                });
            }

            var lokasyonSecebilir =
                await _yetkiService.LokasyonSecebilirAsync(
                    yetkiId,
                    branchId);

            return Json(new
            {
                lokasyonSecebilir
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Yetkilendir(
            [FromBody] YetkilendirmeRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "İstek verisi alınamadı."
                    });
                }

                if (request.YetkiId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Yetki seçimi geçersiz."
                    });
                }

                if (request.BranchId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Şube seçimi geçersiz."
                    });
                }

                if (!await CanManageTargetBranchAsync(request.BranchId))
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        message = "Seçilen şube kendi şirketinizin kapsamında değil."
                    });
                }

                if (request.Permissions == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Yetki bilgileri gönderilmedi."
                    });
                }

                var targetYetki = await _yetkiService.GetYetkiByIdAsync(request.YetkiId);
                if (targetYetki == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Seçilen yetki profili bulunamadı."
                    });
                }

                // Kullanıcının kendi aktif profili/şubesi için Yetkilendirme sayfasını
                // tamamen kapatmasına izin vermeyerek yönetim ekranının kilitlenmesini
                // önlüyoruz. Diğer bütün CRUD izinleri Yetkilendirme ekranından serbestçe
                // yönetilmeye devam eder.
                var currentScope = await _userScopeService.GetAsync();
                if (currentScope != null &&
                    request.YetkiId == currentScope.YetkiId &&
                    request.BranchId == currentScope.ActiveBranchId)
                {
                    var authorizationPage = (await _yetkiService.GetAllSayfalarAsync())
                        .FirstOrDefault(x => string.Equals(
                            x.Url,
                            "/Yetki/Index",
                            StringComparison.OrdinalIgnoreCase));

                    if (authorizationPage != null)
                    {
                        var ownAuthorizationPermission = request.Permissions
                            .FirstOrDefault(x => x.SayfaId == authorizationPage.Id);

                        if (ownAuthorizationPermission == null ||
                            !ownAuthorizationPermission.Goster ||
                            !ownAuthorizationPermission.Guncelle)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = "Kullandığınız yetki profilinin aktif şubedeki Yetkilendirme Görüntüleme ve Güncelle izinlerini kapatamazsınız. Bu işlem yönetim ekranına erişiminizi tamamen kilitler."
                            });
                        }
                    }
                }

                // Tüm izinler tek seferde yüklenir ve tek SaveChanges ile kaydedilir.
                // Bu, özellikle 10+ sayfalı profillerde onlarca SQL round-trip'i ortadan kaldırır.
                var permissionRows = request.Permissions.Select(perm => new YetkiSayfa
                {
                    SayfaId = perm.SayfaId,
                    Ekle = perm.Ekle,
                    Guncelle = perm.Guncelle,
                    Sil = perm.Sil,
                    Goster = perm.Goster
                });

                await _yetkiService.YetkilendirTopluAsync(
                    request.YetkiId,
                    request.BranchId,
                    permissionRows,
                    request.LokasyonSecmeYetkisi);

                _pagePermissionService.InvalidateAll();

                return Ok(new
                {
                    success = true,
                    message = "Yetkiler başarıyla kaydedildi!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        public sealed class YetkilendirmeRequest
        {
            public int YetkiId { get; set; }
            public int BranchId { get; set; }
            public bool LokasyonSecmeYetkisi { get; set; }
            public List<PermissionModel> Permissions { get; set; } = new();
        }

        public sealed class PermissionModel
        {
            public int SayfaId { get; set; }
            public bool Ekle { get; set; }
            public bool Guncelle { get; set; }
            public bool Sil { get; set; }
            public bool Goster { get; set; }
        }
        private async Task<bool> CanManageTargetBranchAsync(int branchId)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
                return false;

            // Lokasyon seçme yetkisi olmayan kullanıcı başka şubeyi Yetkilendirme
            // ekranında dahi hedefleyemez. Kendi aktif şubesini yönetebilir.
            if (branchId == scope.ActiveBranchId)
                return true;

            return scope.CanSelectBranch &&
                   await _userScopeService.IsBranchInCurrentCompanyAsync(branchId);
        }

    }
}
