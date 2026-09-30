using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;
using IsgCevreYonetim.Web.Services;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IsgCevreYonetim.Web.Controllers
{
    public class IsKazasiController : Controller
    {
        private readonly IIsKazasiService _isKazasiService;
        private readonly IOrganizationService _organizationService;
        private readonly IReferenceService _referenceService;
        private readonly IUserScopeService _userScopeService;
        private readonly IIsKazasiArastirmaService _isKazasiArastirmaService;
        private readonly IsKazasiExcelService _isKazasiExcelService;
        private readonly ApplicationDbContext _db;

        public IsKazasiController(
            IIsKazasiService isKazasiService,
            IOrganizationService organizationService,
            IReferenceService referenceService,
            IUserScopeService userScopeService,
            IIsKazasiArastirmaService isKazasiArastirmaService,
            IsKazasiExcelService isKazasiExcelService,
            ApplicationDbContext db)
        {
            _isKazasiService = isKazasiService;
            _organizationService = organizationService;
            _referenceService = referenceService;
            _userScopeService = userScopeService;
            _isKazasiArastirmaService = isKazasiArastirmaService;
            _isKazasiExcelService = isKazasiExcelService;
            _db = db;
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string searchTerm = "",
            string sortBy = "kaza tarihi",
            bool sortDescending = true,
            int? personelId = null,
            int? branchId = null,
            int? departmentId = null,
            int? kayipGunMin = null,
            int? kayipGunMax = null,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var filter = new IsKazasiFilterDto
            {
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDescending = sortDescending,
                PersonelId = personelId,
                BranchId = scope.ActiveBranchId,
                DepartmentId = departmentId,
                KayipGunMin = kayipGunMin,
                KayipGunMax = kayipGunMax,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi
            };

            var result = await _isKazasiService.GetIsKazalariAsync(filter);

            ViewBag.Branches = new List<Branch>
            {
                new() { Id = scope.ActiveBranchId, BranchAdi = scope.ActiveBranchName }
            };
            ViewBag.Departments = await _organizationService.GetDepartmentsByBranchIdAsync(scope.ActiveBranchId);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDescending = sortDescending;
            ViewBag.DepartmentId = departmentId;
            ViewBag.KayipGunMin = kayipGunMin;
            ViewBag.KayipGunMax = kayipGunMax;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            ViewBag.TotalCount = result.TotalCount;

            return View(result.Items);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            await LoadViewBags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            if (!isKazasi.VardiyaId.HasValue || isKazasi.VardiyaId.Value <= 0)
            {
                ModelState.AddModelError("VardiyaId", "Lütfen vardiya seçiniz.");
            }
            else if (!await _db.Vardiyalar.AsNoTracking().AnyAsync(v =>
                         v.VardiyaId == isKazasi.VardiyaId.Value && v.IsActive && !v.IsDeleted))
            {
                ModelState.AddModelError("VardiyaId", "Seçilen vardiya geçerli veya aktif değil.");
            }

            await MudahaleSekliDogrulaAsync(isKazasi.MudahaleSekliId);

            if (!ModelState.IsValid)
            {
                await LoadViewBags(sahitPersonelIds);
                return View(isKazasi);
            }

            try
            {
                var personelDetay = await _isKazasiService.GetPersonelByIdAsync(isKazasi.PersonelId, scope.ActiveBranchId);
                if (personelDetay == null)
                {
                    ModelState.AddModelError("PersonelId", "Seçilen personel aktif şubeye ait değil.");
                    await LoadViewBags(sahitPersonelIds);
                    return View(isKazasi);
                }

                if (personelDetay != null)
                {
                    isKazasi.PersonelAdSoyad = personelDetay.FullName;
                    isKazasi.BranchId = personelDetay.BranchId;
                    isKazasi.DepartmentId = personelDetay.DepartmentId;
                    isKazasi.UnitId = personelDetay.UnitId;
                    isKazasi.GorevId = personelDetay.GorevId;
                    isKazasi.GrupId = personelDetay.GrupId;
                    isKazasi.IseGirisTarihi = personelDetay.IseGirisTarihi;
                }

                if (isKazasi.SorumluAmirPersonelId.HasValue)
                {
                    var amir = await _isKazasiService.GetPersonelByIdAsync(isKazasi.SorumluAmirPersonelId.Value, scope.ActiveBranchId);
                    if (amir == null)
                    {
                        ModelState.AddModelError("SorumluAmirPersonelId", "Seçilen sorumlu amir aktif şubeye ait değil.");
                        await LoadViewBags(sahitPersonelIds);
                        return View(isKazasi);
                    }
                    isKazasi.SorumluAmirAdSoyad = amir.FullName;
                }
                else
                {
                    isKazasi.SorumluAmirAdSoyad = null;
                }

                await _isKazasiService.CreateAsync(isKazasi, dosyalar, sahitPersonelIds, scope.ActiveBranchId);
                TempData["ToastrSuccess"] = "İş kazası başarıyla kaydedildi!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadViewBags(sahitPersonelIds);
                return View(isKazasi);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var isKazasi = await _isKazasiService.GetByIdAsync(id, scope.ActiveBranchId);
            if (isKazasi == null || isKazasi.BranchId != scope.ActiveBranchId)
            {
                TempData["ToastrError"] = "İş kazası bulunamadı!";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(isKazasi.PersonelAdSoyad) && isKazasi.Personel != null)
            {
                isKazasi.PersonelAdSoyad = isKazasi.Personel.FullName;
            }

            await LoadViewBags();
            ViewBag.SelectedSahitler = isKazasi.Sahitler
                .OrderBy(x => x.AdSoyad)
                .Select(x => new SelectListItem(x.AdSoyad, x.PersonelId.ToString(), true))
                .ToList();
            return View(isKazasi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            if (id != isKazasi.Id)
            {
                TempData["ToastrError"] = "Geçersiz iş kazası ID!";
                return RedirectToAction(nameof(Index));
            }

            if (isKazasi.PersonelId <= 0)
            {
                ModelState.AddModelError("PersonelId", "Lütfen bir personel seçin.");
                await LoadViewBags(sahitPersonelIds);
                return View(isKazasi);
            }

            if (!isKazasi.VardiyaId.HasValue || isKazasi.VardiyaId.Value <= 0)
            {
                ModelState.AddModelError("VardiyaId", "Lütfen vardiya seçiniz.");
            }
            else if (!await _db.Vardiyalar.AsNoTracking().AnyAsync(v =>
                         v.VardiyaId == isKazasi.VardiyaId.Value && v.IsActive && !v.IsDeleted))
            {
                ModelState.AddModelError("VardiyaId", "Seçilen vardiya geçerli veya aktif değil.");
            }

            await MudahaleSekliDogrulaAsync(isKazasi.MudahaleSekliId);

            if (!ModelState.IsValid)
            {
                await LoadViewBags(sahitPersonelIds);
                return View(isKazasi);
            }

            try
            {
                var personelDetay = await _isKazasiService.GetPersonelByIdAsync(isKazasi.PersonelId, scope.ActiveBranchId);
                if (personelDetay == null)
                {
                    ModelState.AddModelError("PersonelId", "Seçilen personel aktif şubeye ait değil.");
                    await LoadViewBags(sahitPersonelIds);
                    return View(isKazasi);
                }

                if (personelDetay != null)
                {
                    isKazasi.PersonelAdSoyad = personelDetay.FullName;
                    isKazasi.BranchId = personelDetay.BranchId;
                    isKazasi.DepartmentId = personelDetay.DepartmentId;
                    isKazasi.UnitId = personelDetay.UnitId;
                    isKazasi.GorevId = personelDetay.GorevId;
                    isKazasi.GrupId = personelDetay.GrupId;
                    isKazasi.IseGirisTarihi = personelDetay.IseGirisTarihi;
                }

                if (isKazasi.SorumluAmirPersonelId.HasValue)
                {
                    var amir = await _isKazasiService.GetPersonelByIdAsync(isKazasi.SorumluAmirPersonelId.Value, scope.ActiveBranchId);
                    if (amir == null)
                    {
                        ModelState.AddModelError("SorumluAmirPersonelId", "Seçilen sorumlu amir aktif şubeye ait değil.");
                        await LoadViewBags(sahitPersonelIds);
                        return View(isKazasi);
                    }
                    isKazasi.SorumluAmirAdSoyad = amir.FullName;
                }
                else
                {
                    isKazasi.SorumluAmirAdSoyad = null;
                }

                await _isKazasiService.UpdateAsync(isKazasi, dosyalar, sahitPersonelIds, scope.ActiveBranchId);
                TempData["ToastrSuccess"] = "İş kazası başarıyla güncellendi!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadViewBags(sahitPersonelIds);
                return View(isKazasi);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var isKazasi = await _isKazasiService.GetByIdAsync(id, scope.ActiveBranchId);
            if (isKazasi == null || isKazasi.BranchId != scope.ActiveBranchId)
            {
                TempData["ToastrError"] = "İş kazası bulunamadı!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ArastirmaMaddeleri = await _isKazasiArastirmaService
                .GetSeciliMaddelerAsync(id, scope.ActiveBranchId);
            ViewBag.ArastirmaBilgisi = await _isKazasiArastirmaService
                .GetArastirmaBilgisiAsync(id, scope.ActiveBranchId);

            return View(isKazasi);
        }

        [HttpGet]
        public async Task<IActionResult> ExcelIndir(int id)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var isKazasi = await _isKazasiService.GetByIdAsync(id, scope.ActiveBranchId);
            if (isKazasi == null || isKazasi.BranchId != scope.ActiveBranchId)
                return NotFound();

            var seciliMaddeler = await _isKazasiArastirmaService
                .GetSeciliMaddelerAsync(id, scope.ActiveBranchId);
            var arastirma = await _isKazasiArastirmaService
                .GetArastirmaBilgisiAsync(id, scope.ActiveBranchId);

            // Excel'deki Birincil/Kök Neden kutularını yalnız seçili maddenin Sira
            // değerine bağımlı bırakmıyoruz. Kategorideki tüm maddeleri de alıp
            // gerçek kategori içi konumunu hesaplıyoruz.
            var tumArastirmaMaddeleri = await _isKazasiArastirmaService
                .GetMaddelerAsync(includeInactive: true);

            var duzelticiFaaliyetler = await _db.IsKazasiDuzelticiFaaliyetler
                .AsNoTracking()
                .Where(x => x.IsKazasiId == id && x.IsActive)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();

            var bytes = _isKazasiExcelService.Olustur(
                isKazasi, seciliMaddeler, arastirma, tumArastirmaMaddeleri, duzelticiFaaliyetler);
            var personel = string.IsNullOrWhiteSpace(isKazasi.PersonelAdSoyad)
                ? $"Kaza_{isKazasi.Id}"
                : DosyaAdiTemizle(isKazasi.PersonelAdSoyad);

            var fileName = $"150-FR-513_Ayrintili_Kaza_Arastirma_{personel}_{isKazasi.KazaTarihi:yyyyMMdd}.xls";
            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        private static string DosyaAdiTemizle(string value)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            try
            {
                var scope = await _userScopeService.GetAsync();
                if (scope == null)
                    return Json(new { success = false, message = "Aktif şube bilgisi bulunamadı." });

                await _isKazasiService.DeleteAsync(id, scope.ActiveBranchId);
                return Json(new { success = true, message = "İş kazası başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaSil(int id)
        {
            try
            {
                var scope = await _userScopeService.GetAsync();
                if (scope == null) return Json(new { success = false, message = "Aktif şube bilgisi bulunamadı." });
                var result = await _isKazasiService.DeleteDosyaAsync(id, scope.ActiveBranchId);
                return Json(new { success = result, message = result ? "Dosya başarıyla silindi!" : "Dosya bulunamadı!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchPersonel(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                return Json(new List<object>());

            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Json(new List<object>());

            var result = await _isKazasiService.SearchPersonelAsync(term, scope.ActiveBranchId);
            return Json(result.Select(p => new
            {
                id = p.Id,
                text = $"{p.FullName} ({p.SicilNo})",
                fullName = p.FullName,
                sicilNo = p.SicilNo,
                email = p.Email,
                branchId = p.BranchId,
                branchAdi = p.BranchAdi,
                departmentId = p.DepartmentId,
                departmentAdi = p.DepartmentAdi,
                unitId = p.UnitId,
                unitAdi = p.UnitAdi,
                gorevId = p.GorevId,
                gorevAdi = p.GorevAdi,
                grupId = p.GrupId,
                grupAdi = p.GrupAdi,
                dogumTarihi = p.DogumTarihi?.ToString("yyyy-MM-dd"),
                iseGirisTarihi = p.IseGirisTarihi?.ToString("yyyy-MM-dd")
            }));
        }

        private async Task LoadViewBags(IEnumerable<int>? selectedSahitIds = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
            {
                ViewBag.Branches = new List<Branch>();
                ViewBag.Departments = new List<Department>();
                ViewBag.Units = new List<Unit>();
                ViewBag.Gorevler = await _referenceService.GetAllGorevlerAsync();
                ViewBag.Gruplar = await _referenceService.GetAllGruplarAsync();
                ViewBag.Vardiyalar = await _db.Vardiyalar.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.VardiyaId).ToListAsync();
                ViewBag.MudahaleSekilleri = await _db.MudahaleSekilleri.AsNoTracking()
                    .Where(x => x.IsActive).OrderBy(x => x.Sira).ThenBy(x => x.Ad).ToListAsync();
                ViewBag.SelectedSahitler = new List<SelectListItem>();
                return;
            }

            ViewBag.Branches = new List<Branch>
            {
                new() { Id = scope.ActiveBranchId, BranchAdi = scope.ActiveBranchName }
            };
            ViewBag.Departments = await _organizationService.GetDepartmentsByBranchIdAsync(scope.ActiveBranchId);
            // Önceden her departman için ayrı SQL çalışıyordu (N+1). Artık tek sorgu.
            ViewBag.Units = await _organizationService.GetUnitsByBranchIdAsync(scope.ActiveBranchId);

            ViewBag.Gorevler = await _referenceService.GetAllGorevlerAsync();
            ViewBag.Gruplar = await _referenceService.GetAllGruplarAsync();
            ViewBag.Vardiyalar = await _db.Vardiyalar.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.VardiyaId).ToListAsync();
            ViewBag.MudahaleSekilleri = await _db.MudahaleSekilleri.AsNoTracking()
                .Where(x => x.IsActive).OrderBy(x => x.Sira).ThenBy(x => x.Ad).ToListAsync();
            var sahitIds = (selectedSahitIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            var seciliSahitler = sahitIds.Count == 0
                ? new List<SelectListItem>()
                : (await _db.Personeller.AsNoTracking()
                    .Where(p => sahitIds.Contains(p.Id) && p.BranchId == scope.ActiveBranchId)
                    .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
                    .Select(p => new { Id = p.Id, Text = p.Ad + " " + p.Soyad + " (" + p.SicilNo + ")" })
                    .ToListAsync())
                    .Select(p => new SelectListItem(p.Text, p.Id.ToString(), true))
                    .ToList();
            ViewBag.SelectedSahitler = seciliSahitler;
        }

        private async Task MudahaleSekliDogrulaAsync(int? mudahaleSekliId)
        {
            if (!mudahaleSekliId.HasValue || mudahaleSekliId.Value <= 0)
            {
                ModelState.AddModelError(nameof(IsKazasi.MudahaleSekliId), "Lütfen müdahale şekli seçiniz.");
                return;
            }

            var gecerli = await _db.MudahaleSekilleri.AsNoTracking()
                .AnyAsync(x => x.Id == mudahaleSekliId.Value && x.IsActive && !x.IsDeleted);
            if (!gecerli)
                ModelState.AddModelError(nameof(IsKazasi.MudahaleSekliId), "Seçilen müdahale şekli geçerli veya aktif değil.");
        }

        [HttpGet]
        public async Task<IActionResult> Rapor(
            int? departmentId = null,
            int? branchId = null,
            int? onemliKazaGun = 3,
            int? yil = null,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();
            branchId = scope.ActiveBranchId;

            // Sabit yıl kullanılmaz. Takvim yılı değiştiğinde yeni yıl hem varsayılan
            // seçim olur hem de filtre listesine otomatik eklenir.
            var guncelYil = DateTime.Today.Year;
            var seciliYil = yil.HasValue && yil.Value >= 1900 && yil.Value <= guncelYil
                ? yil.Value
                : guncelYil;
            var yilBaslangici = new DateTime(seciliYil, 1, 1);
            var yilBitisi = new DateTime(seciliYil, 12, 31);

            // Rapor her zaman seçilen yıl ile sınırlandırılır. Kullanıcı ayrıca
            // tarih aralığı verirse bu aralık seçilen yılın içinde daraltılır.
            var etkiliBaslangic = baslangicTarihi.HasValue && baslangicTarihi.Value.Date > yilBaslangici
                ? baslangicTarihi.Value.Date
                : yilBaslangici;
            var etkiliBitis = bitisTarihi.HasValue && bitisTarihi.Value.Date < yilBitisi
                ? bitisTarihi.Value.Date
                : yilBitisi;

            var rapor = await _isKazasiService.GetRaporAsync(
                departmentId,
                branchId,
                onemliKazaGun,
                etkiliBaslangic,
                etkiliBitis);

            var enEskiKayitYili = await _db.IsKazalari
                .AsNoTracking()
                .Where(i => !i.IsDeleted && i.BranchId == scope.ActiveBranchId)
                .Select(i => (int?)i.KazaTarihi.Year)
                .MinAsync() ?? guncelYil;

            var ilkRaporYili = Math.Min(enEskiKayitYili, guncelYil);
            var raporYillari = Enumerable
                .Range(ilkRaporYili, guncelYil - ilkRaporYili + 1)
                .Reverse()
                .ToList();

            ViewBag.Departments = await _organizationService.GetDepartmentsByBranchIdAsync(scope.ActiveBranchId);
            ViewBag.Branches = new List<Branch>
            {
                new() { Id = scope.ActiveBranchId, BranchAdi = scope.ActiveBranchName }
            };
            ViewBag.SelectedDepartmentId = departmentId;
            ViewBag.SelectedBranchId = branchId;
            ViewBag.OnemliKazaGun = onemliKazaGun ?? 3;
            ViewBag.SelectedYear = seciliYil;
            ViewBag.ReportYears = raporYillari;

            return View(rapor);
        }
        // Excel export - ClosedXML
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string searchTerm = "",
            int? personelId = null,
            int? branchId = null,
            int? departmentId = null,
            int? kayipGunMin = null,
            int? kayipGunMax = null,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var filter = new IsKazasiFilterDto
            {
                SearchTerm = searchTerm,
                PersonelId = personelId,
                BranchId = scope.ActiveBranchId,
                DepartmentId = departmentId,
                KayipGunMin = kayipGunMin,
                KayipGunMax = kayipGunMax,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await _isKazasiService.GetIsKazalariAsync(filter);
            var data = result.Items.ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("İş Kazaları");
            var headers = new[]
            {
                "ID", "Kaza Tarihi", "Personel", "Sicil No", "Departman",
                "Şube", "Birim", "Görev", "Grup", "Açıklama",
                "Müdahale", "Rapor Başlangıç", "Rapor Bitiş", "Kayıp Gün", "Dosya"
            };

            worksheet.Cell(1, 1).Value = "İŞ KAZALARI RAPORU";
            worksheet.Range(1, 1, 1, headers.Length).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Oluşturulma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Range(2, 1, 2, headers.Length).Merge();
            worksheet.Cell(2, 1).Style.Font.Italic = true;

            var headerRow = 4;
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            for (var index = 0; index < data.Count; index++)
            {
                var item = data[index];
                var row = headerRow + index + 1;
                var values = new object?[]
                {
                    item.Id,
                    item.KazaTarihi.ToString("dd.MM.yyyy HH:mm"),
                    item.PersonelAdSoyad ?? "",
                    item.Personel?.SicilNo ?? "",
                    item.Department?.DepartmentAdi ?? "",
                    item.Branch?.BranchAdi ?? "",
                    item.Unit?.UnitAdi ?? "",
                    item.Gorev?.Ad ?? "",
                    item.Grup?.Ad ?? "",
                    item.Aciklama ?? "",
                    item.Mudahale ?? "",
                    item.RaporBaslangic?.ToString("dd.MM.yyyy") ?? "",
                    item.RaporBitis?.ToString("dd.MM.yyyy") ?? "",
                    item.KayipGun,
                    item.Dosyalar?.Count ?? 0
                };

                for (var column = 0; column < values.Length; column++)
                {
                    worksheet.Cell(row, column + 1).Value = XLCellValue.FromObject(values[column]);
                    worksheet.Cell(row, column + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
            }

            var summaryRow = headerRow + data.Count + 3;
            worksheet.Cell(summaryRow, 1).Value = "ÖZET";
            worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;
            worksheet.Cell(summaryRow + 1, 1).Value = $"Toplam Kaza: {data.Count}";
            worksheet.Cell(summaryRow + 2, 1).Value = $"Toplam Kayıp Gün: {data.Sum(x => x.KayipGun ?? 0)}";
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"IsKazalari_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ⭐⭐⭐ PDF EXPORT - iText7 ile ⭐⭐⭐
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            string searchTerm = "",
            int? personelId = null,
            int? branchId = null,
            int? departmentId = null,
            int? kayipGunMin = null,
            int? kayipGunMax = null,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var filter = new IsKazasiFilterDto
            {
                SearchTerm = searchTerm,
                PersonelId = personelId,
                BranchId = scope.ActiveBranchId,
                DepartmentId = departmentId,
                KayipGunMin = kayipGunMin,
                KayipGunMax = kayipGunMax,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await _isKazasiService.GetIsKazalariAsync(filter);
            var data = result.Items.ToList();

            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, PageSize.A4.Rotate());

            // Fontlar
            var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Başlık
            var title = new Paragraph("İŞ KAZALARI RAPORU")
                .SetFont(titleFont)
                .SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(10);
            document.Add(title);

            // Tarih
            document.Add(new Paragraph($"Oluşturulma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}")
                .SetFont(normalFont)
                .SetFontSize(10)
                .SetFontColor(ColorConstants.GRAY));

            document.Add(new Paragraph(" "));

            // Filtre Bilgileri
            if (!string.IsNullOrEmpty(searchTerm))
            {
                document.Add(new Paragraph($"Arama: {searchTerm}").SetFont(normalFont).SetFontSize(10));
            }
            if (baslangicTarihi.HasValue)
            {
                document.Add(new Paragraph($"Tarih Aralığı: {baslangicTarihi.Value:dd.MM.yyyy} - {(bitisTarihi ?? DateTime.Now):dd.MM.yyyy}").SetFont(normalFont).SetFontSize(10));
            }
            document.Add(new Paragraph(" "));

            // Tablo
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 4, 9, 11, 7, 9, 9, 7, 7, 7, 11, 11, 9, 9, 7, 5 }));
            table.SetWidth(UnitValue.CreatePercentValue(100));

            // Header
            var headers = new[] {
                "ID", "Kaza Tarihi", "Personel", "Sicil No", "Departman",
                "Şube", "Birim", "Görev", "Grup", "Açıklama",
                "Müdahale", "Rapor Başlangıç", "Rapor Bitiş", "Kayıp Gün", "Dosya"
            };

            foreach (var header in headers)
            {
                var cell = new Cell()
                    .Add(new Paragraph(header).SetFont(headerFont).SetFontSize(9))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetPadding(5);
                table.AddCell(cell);
            }

            // Veriler
            foreach (var item in data)
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Id.ToString()).SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(item.KazaTarihi.ToString("dd.MM.yyyy HH:mm")).SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(item.PersonelAdSoyad ?? "-").SetFont(normalFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Personel?.SicilNo ?? "-").SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(item.Department?.DepartmentAdi ?? "-").SetFont(normalFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Branch?.BranchAdi ?? "-").SetFont(normalFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Unit?.UnitAdi ?? "-").SetFont(normalFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Gorev?.Ad ?? "-").SetFont(normalFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Grup?.Ad ?? "-").SetFont(normalFont).SetFontSize(8)));

                var aciklama = (item.Aciklama ?? "-");
                if (aciklama.Length > 30) aciklama = aciklama.Substring(0, 30) + "...";
                table.AddCell(new Cell().Add(new Paragraph(aciklama).SetFont(normalFont).SetFontSize(8)));

                var mudahale = (item.Mudahale ?? "-");
                if (mudahale.Length > 30) mudahale = mudahale.Substring(0, 30) + "...";
                table.AddCell(new Cell().Add(new Paragraph(mudahale).SetFont(normalFont).SetFontSize(8)));

                table.AddCell(new Cell().Add(new Paragraph(item.RaporBaslangic?.ToString("dd.MM.yyyy") ?? "-").SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(item.RaporBitis?.ToString("dd.MM.yyyy") ?? "-").SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(item.KayipGun?.ToString() ?? "-").SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph((item.Dosyalar?.Count ?? 0).ToString()).SetFont(normalFont).SetFontSize(8)).SetTextAlignment(TextAlignment.CENTER));
            }

            document.Add(table);

            // Özet
            document.Add(new Paragraph(" "));
            var summaryFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            document.Add(new Paragraph($"Toplam Kaza: {data.Count}").SetFont(summaryFont).SetFontSize(11));
            document.Add(new Paragraph($"Toplam Kayıp Gün: {data.Sum(x => x.KayipGun ?? 0)}").SetFont(summaryFont).SetFontSize(11));

            document.Close();

            var fileName = $"IsKazalari_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }
    }
}
