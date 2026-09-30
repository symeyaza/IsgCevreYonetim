using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using IsgCevreYonetim.Web.Services;
using IsgCevreYonetim.Web.ViewModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Security.Claims;

namespace IsgCevreYonetim.Web.Controllers
{
    [Authorize]
    public class PersonelController : Controller
    {
        private readonly IPersonelService _personelService;
        private readonly IReferenceService _referenceService;
        private readonly IOrganizationService _organizationService;
        private readonly IYetkiService _yetkiService;
        private readonly IUserScopeService _userScopeService;
        private readonly ApplicationDbContext _context;

        public PersonelController(
            IPersonelService personelService,
            IReferenceService referenceService,
            IOrganizationService organizationService,
            IYetkiService yetkiService,
            IUserScopeService userScopeService,
            ApplicationDbContext context)
        {
            _personelService = personelService;
            _referenceService = referenceService;
            _organizationService = organizationService;
            _yetkiService = yetkiService;
            _userScopeService = userScopeService;
            _context = context;
        }

        // =========================================================
        // INDEX
        // =========================================================

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string searchTerm = "",
            string sortBy = "ad",
            bool sortDescending = false,
            bool? isActive = null,
            int? companyId = null,
            int? branchId = null,
            int? yetkiId = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var filter = new PersonelFilterDto
            {
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDescending = sortDescending,
                IsActive = isActive,
                CompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId,
                BranchId = scope.ActiveBranchId,
                YetkiId = yetkiId
            };

            var result =
                await _personelService.GetPersonellerAsync(filter);

            // Şirket/şube adı zaten request scope hesaplanırken elde edildi. Index için
            // aynı kayıtları tekrar SQL'den okumuyoruz.
            var activeCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            ViewBag.Companies = activeCompanyId > 0
                ? new List<Company> { new Company { Id = activeCompanyId, CompanyAdi = scope.ActiveCompanyName } }
                : new List<Company>();
            ViewBag.Branches = scope.ActiveBranchId > 0
                ? new List<Branch> { new Branch { Id = scope.ActiveBranchId, CompanyId = activeCompanyId, BranchAdi = scope.ActiveBranchName } }
                : new List<Branch>();

            ViewBag.Yetkiler =
                await _yetkiService.GetAllYetkilerAsync();

            // Sayfalama / filtre
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDescending = sortDescending;
            ViewBag.IsActive = isActive;
            ViewBag.CompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            ViewBag.BranchId = scope.ActiveBranchId;
            ViewBag.YetkiId = yetkiId;

            ViewBag.TotalPages =
                (int)Math.Ceiling(
                    (double)result.TotalCount / pageSize);

            ViewBag.TotalCount =
                result.TotalCount;

            return View(result.Items);
        }

        [HttpGet]
        public async Task<IActionResult> Rapor(int? yil = null, int? departmanId = null, bool? aktif = null, int sayfa = 1)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();
            sayfa = Math.Max(1, sayfa);
            var today = DateTime.Today;
            yil = (yil is >= 1900 and <= 9998) && yil <= DateTime.Today.Year ? yil : null;
            var query = PersonelRaporSorgusu(scope.ActiveBranchId, yil, departmanId, aktif);
            var total = await query.CountAsync();
            sayfa = Math.Min(sayfa, Math.Max(1, (int)Math.Ceiling(total / 25d)));
            var active = await query.CountAsync(p => p.AktifMi && p.IsActive);
            var firstHire = await _context.Personeller.AsNoTracking()
                .Where(p => p.BranchId == scope.ActiveBranchId && p.IseGirisTarihi != null)
                .MinAsync(p => p.IseGirisTarihi);
            var firstYear = Math.Clamp(firstHire?.Year ?? today.Year, 1900, today.Year);
            var year = yil ?? today.Year;
            var monthly = await query.Where(p => p.IseGirisTarihi >= new DateTime(year, 1, 1) && p.IseGirisTarihi < new DateTime(year + 1, 1, 1))
                .GroupBy(p => p.IseGirisTarihi!.Value.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() }).ToListAsync();
            var departments = await query.GroupBy(p => p.Department != null ? p.Department.DepartmentAdi : "Atanmamış")
                .OrderByDescending(g => g.Count()).Select(g => new RaporDagilimi(g.Key, g.Count())).ToListAsync();
            var rows = await query.OrderBy(p => p.Ad).ThenBy(p => p.Soyad).ThenBy(p => p.Id)
                .Skip((sayfa - 1) * 25).Take(25)
                .Select(p => new PersonelRaporSatiri(p.Id, p.SicilNo, p.Ad + " " + p.Soyad,
                    p.Department != null ? p.Department.DepartmentAdi : "—",
                    p.Unit != null ? p.Unit.UnitAdi : "—",
                    p.Yetki != null ? p.Yetki.Ad : "—", p.IseGirisTarihi, p.AktifMi && p.IsActive))
                .ToListAsync();
            var model = new PersonelRaporViewModel
            {
                Sube = scope.ActiveBranchName, Yil = yil, DepartmanId = departmanId, Aktif = aktif,
                Toplam = total, AktifSayisi = active,
                BuYilBaslayan = monthly.Sum(x => x.Count),
                Yillar = Enumerable.Range(firstYear, today.Year - firstYear + 1).Reverse().ToList(),
                Departmanlar = await _context.Departments.AsNoTracking().Where(d => d.BranchId == scope.ActiveBranchId)
                    .OrderBy(d => d.DepartmentAdi).Select(d => new RaporSecenegi(d.Id, d.DepartmentAdi)).ToListAsync(),
                DepartmanDagilimi = departments, Satirlar = rows,
                Sayfa = sayfa, SayfaSayisi = (int)Math.Ceiling(total / 25d)
            };
            foreach (var month in monthly) model.AylikGiris[month.Month - 1] = month.Count;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RaporExcel(int? yil = null, int? departmanId = null, bool? aktif = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();
            yil = (yil is >= 1900 and <= 9998) && yil <= DateTime.Today.Year ? yil : null;
            var reportQuery = PersonelRaporSorgusu(scope.ActiveBranchId, yil, departmanId, aktif);
            if (await reportQuery.Take(50001).CountAsync() > 50000) return BadRequest("Excel en fazla 50.000 kayıt içerir; filtreyi daraltın.");
            var rows = await reportQuery
                .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
                .Select(p => new PersonelRaporSatiri(p.Id, p.SicilNo, p.Ad + " " + p.Soyad,
                    p.Department != null ? p.Department.DepartmentAdi : "—",
                    p.Unit != null ? p.Unit.UnitAdi : "—",
                    p.Yetki != null ? p.Yetki.Ad : "—", p.IseGirisTarihi, p.AktifMi && p.IsActive))
                .ToListAsync();
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var sheet = workbook.Worksheets.Add("Personel Raporu");
            var headers = new[] { "Sicil No", "Ad Soyad", "Departman", "Birim", "Yetki", "İşe Giriş", "Durum" };
            for (var col = 0; col < headers.Length; col++) sheet.Cell(1, col + 1).Value = headers[col];
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; var n = i + 2;
                sheet.Cell(n, 1).Value = row.SicilNo; sheet.Cell(n, 2).Value = row.AdSoyad;
                sheet.Cell(n, 3).Value = row.Departman; sheet.Cell(n, 4).Value = row.Birim;
                sheet.Cell(n, 5).Value = row.Yetki;
                if (row.IseGirisTarihi.HasValue) { sheet.Cell(n, 6).Value = row.IseGirisTarihi.Value; sheet.Cell(n, 6).Style.DateFormat.Format = "dd.MM.yyyy"; }
                sheet.Cell(n, 7).Value = row.Aktif ? "Aktif" : "Pasif";
            }
            sheet.Range(1, 1, 1, 7).Style.Font.Bold = true;
            sheet.Columns(1, 7).AdjustToContents();
            using var output = new MemoryStream(); workbook.SaveAs(output);
            return File(output.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Personel_Raporu.xlsx");
        }

        private IQueryable<Personel> PersonelRaporSorgusu(int branchId, int? yil, int? departmanId, bool? aktif)
        {
            var query = _context.Personeller.AsNoTracking().Where(p => p.BranchId == branchId);
            if ((yil is >= 1900 and <= 9998) && yil <= DateTime.Today.Year)
            {
                var start = new DateTime(yil.Value, 1, 1);
                var end = start.AddYears(1);
                query = query.Where(p => p.IseGirisTarihi >= start && p.IseGirisTarihi < end);
            }
            if (departmanId.HasValue) query = query.Where(p => p.DepartmentId == departmanId.Value);
            if (aktif.HasValue) query = query.Where(p => (p.AktifMi && p.IsActive) == aktif.Value);
            return query;
        }

        // =========================================================
        // PERSONEL EKLE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            await LoadReferenceData();

            // Yeni personel formunda yalnız organizasyon kapsamı önceden belirlenir.
            // Email, sicil, şifre vb. kullanıcı alanları boş kalır.
            return View(new Personel
            {
                CompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId,
                BranchId = scope.ActiveBranchId
            });
        }

        // =========================================================
        // PERSONEL EKLE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            Personel personel,
            string password,
            IFormFile? profilResmi)
        {
            ModelState.Remove("SifreHash");
            ModelState.Remove("SifreSalt");

            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            personel.CompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            personel.BranchId = scope.ActiveBranchId;

            if (personel.DogumTarihi.HasValue && personel.DogumTarihi.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(Personel.DogumTarihi), "Doğum tarihi bugünden ileri bir tarih olamaz.");
                await LoadReferenceData(personel);
                return View(personel);
            }

            if (!await ValidatePersonelOrganizationSelectionsAsync(personel, scope))
            {
                ModelState.AddModelError("", "Şirket, şube, departman veya birim seçimi birbiriyle uyumlu değil.");
                await LoadReferenceData(personel);
                return View(personel);
            }

            // -----------------------------------------------------
            // AD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Ad))
            {
                ModelState.AddModelError(
                    "Ad",
                    "Ad zorunludur!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // SOYAD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Soyad))
            {
                ModelState.AddModelError(
                    "Soyad",
                    "Soyad zorunludur!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // EMAIL
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Email))
            {
                ModelState.AddModelError(
                    "Email",
                    "Email zorunludur!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            if (!IsValidEmail(personel.Email))
            {
                ModelState.AddModelError(
                    "Email",
                    "Geçerli bir email adresi girin!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // SİCİL NO
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.SicilNo))
            {
                ModelState.AddModelError(
                    "SicilNo",
                    "Sicil No zorunludur!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // ŞİFRE
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(password) ||
                password.Length < 6)
            {
                ModelState.AddModelError(
                    "",
                    "Şifre en az 6 karakter olmalıdır!");

                await LoadReferenceData(personel);

                return View(personel);
            }

            // Email ve SicilNo için veritabanında UNIQUE index vardır. Başarılı kayıtların
            // tamamında fazladan SELECT çalıştırmak yerine doğruluğun tek kaynağı DB'dir.
            // Olası unique ihlali PersonelService.SaveChanges sırasında kullanıcı dostu mesaja çevrilir.

            try
            {
                personel.CreatedDate = DateTime.Now;
                personel.IsActive = true;
                personel.IsDeleted = false;

                var createdPersonel =
                    await _personelService.CreateAsync(
                        personel,
                        password);

                // -------------------------------------------------
                // PROFİL RESMİ
                // -------------------------------------------------

                if (profilResmi != null &&
                    profilResmi.Length > 0)
                {
                    try
                    {
                        var savedProfilePath = await _personelService
                            .SaveProfileImageAsync(
                                createdPersonel.Id,
                                profilResmi);

                        var currentPersonelId = GetCurrentPersonelId();
                        if (currentPersonelId.HasValue &&
                            currentPersonelId.Value == createdPersonel.Id)
                        {
                            // SaveProfileImageAsync zaten yeni yolu döndürür; tekrar DB okuma yok.
                            await UpdateProfileImageClaimAsync(savedProfilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["ToastrWarning"] =
                            $"Personel eklendi ancak profil resmi " +
                            $"yüklenemedi: {ex.Message}";
                    }
                }

                TempData["ToastrSuccess"] =
                    "Personel başarıyla eklendi!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    $"Kayıt sırasında hata oluştu: {ex.Message}");

                await LoadReferenceData(personel);

                return View(personel);
            }
        }

        // =========================================================
        // PERSONEL DÜZENLE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var personel =
                await _personelService
                    .GetPersonelWithDetailsAsync(id);

            if (personel == null)
            {
                TempData["ToastrError"] =
                    "Personel bulunamadı!";

                return RedirectToAction(nameof(Index));
            }

            var scope = await _userScopeService.GetAsync();
            var activeCompanyId = scope?.IsSystemAdmin == true ? scope.ActiveCompanyId : scope?.CompanyId;
            if (scope == null || personel.CompanyId != activeCompanyId || personel.BranchId != scope.ActiveBranchId)
                return Forbid();

            await LoadReferenceData(personel);

            return View(personel);
        }

        // =========================================================
        // PERSONEL DÜZENLE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(
            Personel personel,
            IFormFile? profilResmi,
            string? newPassword,
            string? passwordConfirm)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null) return Forbid();

            var scopedExisting = await _personelService.GetByIdAsync(personel.Id);
            var activeCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            var editingSystemAdminSelf = scope.IsSystemAdmin && scope.PersonelId == personel.Id;
            if (scopedExisting == null || (!editingSystemAdminSelf &&
                (scopedExisting.CompanyId != activeCompanyId || scopedExisting.BranchId != scope.ActiveBranchId)))
                return Forbid();

            if (personel.DogumTarihi.HasValue && personel.DogumTarihi.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(Personel.DogumTarihi), "Doğum tarihi bugünden ileri bir tarih olamaz.");
                await LoadReferenceData(personel);
                return View(personel);
            }

            if (editingSystemAdminSelf)
            {
                // Süper Admin kendi kaydında hiçbir organizasyona kalıcı olarak bağlanmaz.
                // Operasyonel şirket/şube Dashboard seçiminden gelir.
                personel.CompanyId = null;
                personel.BranchId = null;
                personel.DepartmentId = null;
                personel.UnitId = null;
            }
            else
            {
                personel.CompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
                personel.BranchId = scope.ActiveBranchId;
            }

            if (!editingSystemAdminSelf && !await ValidatePersonelOrganizationSelectionsAsync(personel, scope))
            {
                ModelState.AddModelError("", "Şirket, şube, departman veya birim seçimi birbiriyle uyumlu değil.");
                await LoadReferenceData(personel);
                return View(personel);
            }

            // -----------------------------------------------------
            // AD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Ad))
            {
                TempData["ToastrError"] =
                    "Ad zorunludur!";

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // SOYAD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Soyad))
            {
                TempData["ToastrError"] =
                    "Soyad zorunludur!";

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // EMAIL
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(personel.Email))
            {
                TempData["ToastrError"] =
                    "Email zorunludur!";

                await LoadReferenceData(personel);

                return View(personel);
            }

            if (!IsValidEmail(personel.Email))
            {
                TempData["ToastrError"] =
                    "Geçerli bir email adresi girin!";

                await LoadReferenceData(personel);

                return View(personel);
            }

            // -----------------------------------------------------
            // ŞİFRE (OPSİYONEL)
            // -----------------------------------------------------
            // Her iki alan da boşsa kullanıcı şifresini değiştirmek istemiyor demektir.
            // Alanlardan biri doldurulmuşsa ikisi de zorunludur ve eşleşmelidir.
            var wantsPasswordChange =
                !string.IsNullOrWhiteSpace(newPassword) ||
                !string.IsNullOrWhiteSpace(passwordConfirm);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(newPassword) ||
                    string.IsNullOrWhiteSpace(passwordConfirm))
                {
                    ModelState.AddModelError("", "Yeni şifre ve şifre tekrarı birlikte doldurulmalıdır.");
                    await LoadReferenceData(personel);
                    return View(personel);
                }

                if (newPassword != passwordConfirm)
                {
                    ModelState.AddModelError("", "Yeni şifre ve şifre tekrarı eşleşmiyor.");
                    await LoadReferenceData(personel);
                    return View(personel);
                }

                if (!IsStrongPassword(newPassword))
                {
                    ModelState.AddModelError("",
                        "Şifre en az 6 karakter olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.");
                    await LoadReferenceData(personel);
                    return View(personel);
                }
            }

            // Email/SicilNo benzersizliği DB UNIQUE indexleri tarafından atomik olarak korunur.
            // Başarılı güncelleme yolunda gereksiz bir SQL round-trip yapılmaz.

            // scopedExisting zaten bu request içinde yüklenmiş ve DbContext tarafından
            // takip ediliyor; aynı personeli tekrar SELECT etmiyoruz.
            var existingPersonel = scopedExisting;

            // -----------------------------------------------------
            // PROFİL RESMİ
            // -----------------------------------------------------

            if (profilResmi != null &&
                profilResmi.Length > 0)
            {
                try
                {
                    personel.ProfilResmi = await _personelService
                        .SaveProfileImageAsync(
                            personel.Id,
                            profilResmi);

                    // =============================================
                    // KENDİ PROFİLİ İSE COOKIE CLAIM'İNİ YENİLE
                    // =============================================

                    var currentPersonelId =
                        GetCurrentPersonelId();

                    if (currentPersonelId.HasValue &&
                        currentPersonelId.Value ==
                        personel.Id)
                    {
                        await UpdateProfileImageClaimAsync(
                            personel.ProfilResmi ?? "");
                    }
                }
                catch (Exception ex)
                {
                    TempData["ToastrWarning"] =
                        $"Profil resmi yüklenemedi: {ex.Message}";

                    personel.ProfilResmi =
                        existingPersonel.ProfilResmi;
                }
            }
            else
            {
                personel.ProfilResmi =
                    existingPersonel.ProfilResmi;
            }

            // Şifre/audit alanları PersonelService.UpdateAsync içinde mevcut tracked
            // entity üzerinde korunur; POST modeline geri kopyalamaya gerek yok.
            personel.UpdatedDate = DateTime.Now;

            await _personelService
                .UpdateAsync(personel, wantsPasswordChange ? newPassword : null);

            // -----------------------------------------------------
            // GİRİŞ YAPAN KULLANICI KENDİ BİLGİLERİNİ
            // DEĞİŞTİRDİYSE CLAIM'LERİ YENİLE
            // -----------------------------------------------------

            var loggedInPersonelId =
                GetCurrentPersonelId();

            if (loggedInPersonelId.HasValue &&
                loggedInPersonelId.Value == personel.Id)
            {
                await RefreshCurrentUserClaimsAsync(
                    personel.Id);
            }

            TempData["ToastrSuccess"] =
                "Personel başarıyla güncellendi!";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DETAY
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var personel =
                await _personelService
                    .GetPersonelWithDetailsAsync(id);

            if (personel == null)
            {
                TempData["ToastrError"] =
                    "Personel bulunamadı!";

                return RedirectToAction(nameof(Index));
            }

            var scope = await _userScopeService.GetAsync();
            var activeCompanyId = scope?.IsSystemAdmin == true ? scope.ActiveCompanyId : scope?.CompanyId;
            if (scope == null || personel.CompanyId != activeCompanyId || personel.BranchId != scope.ActiveBranchId)
                return Forbid();

            return View(personel);
        }

        // =========================================================
        // PROFİL RESMİ SİL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilResmiSil(int id)
        {
            try
            {
                var scope = await _userScopeService.GetAsync();
                var target = await _personelService.GetByIdAsync(id);
                var activeCompanyId = scope?.IsSystemAdmin == true ? scope.ActiveCompanyId : scope?.CompanyId;
                if (scope == null || target == null || target.CompanyId != activeCompanyId || target.BranchId != scope.ActiveBranchId)
                    return Json(new { success = false, message = "Bu personele erişim yetkiniz bulunmuyor." });

                var result =
                    await _personelService
                        .DeleteProfileImageAsync(id);

                if (result)
                {
                    var currentPersonelId =
                        GetCurrentPersonelId();

                    // Kendi profil resmi silindiyse
                    // Session yerine claim'i güncelle.
                    if (currentPersonelId.HasValue &&
                        currentPersonelId.Value == id)
                    {
                        await UpdateProfileImageClaimAsync("");
                    }

                    return Json(new
                    {
                        success = true,
                        message =
                            "Profil resmi başarıyla silindi!"
                    });
                }

                return Json(new
                {
                    success = false,
                    message =
                        "Profil resmi bulunamadı!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        $"Profil resmi silinirken hata oluştu: " +
                        $"{ex.Message}"
                });
            }
        }

        // =========================================================
        // PERSONEL SİL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            try
            {
                // -------------------------------------------------
                // KENDİ KENDİNİ SİLME KONTROLÜ
                // -------------------------------------------------

                var currentPersonelId =
                    GetCurrentPersonelId();

                if (currentPersonelId.HasValue &&
                    currentPersonelId.Value == id)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Giriş yaptığınız kendi kullanıcı " +
                            "hesabınızı silemezsiniz!"
                    });
                }

                var personel =
                    await _personelService
                        .GetByIdAsync(id);

                if (personel == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Personel bulunamadı!"
                    });
                }

                var scope = await _userScopeService.GetAsync();
                var activeCompanyId = scope?.IsSystemAdmin == true ? scope.ActiveCompanyId : scope?.CompanyId;
                if (scope == null || personel.CompanyId != activeCompanyId || personel.BranchId != scope.ActiveBranchId)
                {
                    return Json(new { success = false, message = "Bu personele erişim yetkiniz bulunmuyor." });
                }

                // DeleteAsync profil dosyasını da DB silme başarılı olduktan sonra temizler.
                // Ayrı DeleteProfileImageAsync çağrısı ekstra SELECT + UPDATE + SaveChanges
                // ürettiği için kaldırıldı.
                await _personelService.DeleteAsync(id);

                return Json(new
                {
                    success = true,
                    message =
                        "Personel başarıyla silindi!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        $"Silme sırasında hata oluştu: " +
                        $"{ex.Message}"
                });
            }
        }

        // =========================================================
        // REFERANS VERİLERİ
        // =========================================================

        private async Task LoadReferenceData(Personel? selected = null)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
            {
                ViewBag.Cinsiyetler = new List<Cinsiyet>();
                ViewBag.Gorevler = new List<Gorev>();
                ViewBag.Gruplar = new List<Grup>();
                ViewBag.Companies = new List<Company>();
                ViewBag.Branches = new List<Branch>();
                ViewBag.Departments = new List<Department>();
                ViewBag.Units = new List<Unit>();
                ViewBag.Yetkiler = new List<Yetki>();
                return;
            }

            int? selectedCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            int? selectedBranchId = scope.ActiveBranchId;
            int? selectedDepartmentId = selected?.DepartmentId;

            // Personel formunun tüm lookup verilerini TEK SQL round-trip ile al.
            // Uzak SQL hostinginde 6-9 küçük sorgunun sırayla çalışması, sorgular hızlı olsa
            // bile ağ gecikmesini topluyordu. UNION ALL projection ile Cinsiyet/Görev/Grup/Yetki
            // ve organizasyon seçenekleri tek komutta gelir. CRUD sonrası DB doğrudan okunduğu
            // için eski cache verisi problemi de oluşmaz.
            IQueryable<PersonelFormLookupRow> lookups = _context.Cinsiyetler
                .AsNoTracking()
                .Select(x => new PersonelFormLookupRow { Kind = "C", Id = x.Id, ParentId = null, Text = x.Ad });

            lookups = lookups.Concat(_context.Gorevler
                .AsNoTracking()
                .Select(x => new PersonelFormLookupRow { Kind = "G", Id = x.Id, ParentId = null, Text = x.Ad }));

            lookups = lookups.Concat(_context.Gruplar
                .AsNoTracking()
                .Select(x => new PersonelFormLookupRow { Kind = "R", Id = x.Id, ParentId = null, Text = x.Ad }));

            var yetkiQuery = _context.Yetkiler.AsNoTracking().Where(x => !x.IsDeleted);
            if (!scope.IsSystemAdmin)
                yetkiQuery = yetkiQuery.Where(x => x.Ad != "Süper Admin");

            lookups = lookups.Concat(yetkiQuery
                .Select(x => new PersonelFormLookupRow { Kind = "Y", Id = x.Id, ParentId = null, Text = x.Ad }));

            if (scope.IsSystemAdmin)
            {
                var companyId = scope.ActiveCompanyId;
                var branchId = scope.ActiveBranchId;
                lookups = lookups.Concat(_context.Companies
                    .AsNoTracking()
                    .Where(x => x.Id == companyId && !x.IsDeleted)
                    .Select(x => new PersonelFormLookupRow { Kind = "CO", Id = x.Id, ParentId = null, Text = x.CompanyAdi }));
                lookups = lookups.Concat(_context.Branches
                    .AsNoTracking()
                    .Where(x => x.Id == branchId && x.CompanyId == companyId && !x.IsDeleted)
                    .Select(x => new PersonelFormLookupRow { Kind = "B", Id = x.Id, ParentId = x.CompanyId, Text = x.BranchAdi }));
            }

            if (selectedBranchId.HasValue && selectedBranchId.Value > 0)
            {
                var branchId = selectedBranchId.Value;
                lookups = lookups.Concat(_context.Departments
                    .AsNoTracking()
                    .Where(x => x.BranchId == branchId && !x.IsDeleted)
                    .Select(x => new PersonelFormLookupRow { Kind = "D", Id = x.Id, ParentId = x.BranchId, Text = x.DepartmentAdi }));
            }

            if (selectedDepartmentId.HasValue && selectedDepartmentId.Value > 0)
            {
                var departmentId = selectedDepartmentId.Value;
                lookups = lookups.Concat(_context.Units
                    .AsNoTracking()
                    .Where(x => x.DepartmentId == departmentId && !x.IsDeleted && x.IsActive)
                    .Select(x => new PersonelFormLookupRow { Kind = "U", Id = x.Id, ParentId = x.DepartmentId, Text = x.UnitAdi }));
            }

            // Personel formunda Departman -> Birim değişimini sıfır ek HTTP/SQL beklemesiyle yapmak için
            // aktif şubenin küçük birim sözlüğünü aynı lookup SQL komutuna ekle.
            if (selectedBranchId.HasValue && selectedBranchId.Value > 0)
            {
                var branchIdForUnits = selectedBranchId.Value;
                lookups = lookups.Concat(_context.Units
                    .AsNoTracking()
                    .Where(x => x.IsActive && !x.IsDeleted &&
                                x.Department.IsActive && !x.Department.IsDeleted &&
                                x.Department.BranchId == branchIdForUnits)
                    .Select(x => new PersonelFormLookupRow { Kind = "UA", Id = x.Id, ParentId = x.DepartmentId, Text = x.UnitAdi }));
            }

            var rows = await lookups.ToListAsync();

            ViewBag.Cinsiyetler = rows.Where(x => x.Kind == "C").OrderBy(x => x.Text)
                .Select(x => new Cinsiyet { Id = x.Id, Ad = x.Text }).ToList();
            ViewBag.Gorevler = rows.Where(x => x.Kind == "G").OrderBy(x => x.Text)
                .Select(x => new Gorev { Id = x.Id, Ad = x.Text }).ToList();
            ViewBag.Gruplar = rows.Where(x => x.Kind == "R").OrderBy(x => x.Text)
                .Select(x => new Grup { Id = x.Id, Ad = x.Text }).ToList();
            ViewBag.Yetkiler = rows.Where(x => x.Kind == "Y").OrderBy(x => x.Text)
                .Select(x => new Yetki { Id = x.Id, Ad = x.Text }).ToList();

            if (scope.IsSystemAdmin)
            {
                ViewBag.Companies = rows.Where(x => x.Kind == "CO").OrderBy(x => x.Text)
                    .Select(x => new Company { Id = x.Id, CompanyAdi = x.Text }).ToList();
                ViewBag.Branches = rows.Where(x => x.Kind == "B").OrderBy(x => x.Text)
                    .Select(x => new Branch { Id = x.Id, CompanyId = x.ParentId ?? 0, BranchAdi = x.Text }).ToList();
            }
            else
            {
                ViewBag.Companies = new List<Company>
                {
                    new Company { Id = scope.CompanyId, CompanyAdi = scope.CompanyName }
                };
                ViewBag.Branches = new List<Branch>
                {
                    new Branch { Id = scope.ActiveBranchId, CompanyId = scope.CompanyId, BranchAdi = scope.ActiveBranchName }
                };
            }

            ViewBag.Departments = rows.Where(x => x.Kind == "D").OrderBy(x => x.Text)
                .Select(x => new Department { Id = x.Id, BranchId = x.ParentId ?? 0, DepartmentAdi = x.Text }).ToList();
            ViewBag.Units = rows.Where(x => x.Kind == "U").OrderBy(x => x.Text)
                .Select(x => new Unit { Id = x.Id, DepartmentId = x.ParentId ?? 0, UnitAdi = x.Text }).ToList();
            ViewBag.AllBranchUnits = rows.Where(x => x.Kind == "UA").OrderBy(x => x.Text)
                .Select(x => new Unit { Id = x.Id, DepartmentId = x.ParentId ?? 0, UnitAdi = x.Text }).ToList();
        }

        private sealed class PersonelFormLookupRow
        {
            public string Kind { get; set; } = string.Empty;
            public int Id { get; set; }
            public int? ParentId { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private async Task<bool> ValidatePersonelOrganizationSelectionsAsync(Personel personel, UserScopeInfo scope)
        {
            if (!personel.CompanyId.HasValue || !personel.BranchId.HasValue)
                return false;

            var activeCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            if (personel.CompanyId.Value != activeCompanyId || personel.BranchId.Value != scope.ActiveBranchId)
                return false;

            // Şirket→Şube→Departman→Birim zincirini 3-4 ayrı sorgu yerine
            // tek SQL EXISTS sorgusunda doğrula.
            return await _personelService.ValidateOrganizationSelectionAsync(
                personel.CompanyId.Value,
                personel.BranchId.Value,
                personel.DepartmentId,
                personel.UnitId);
        }

        // =========================================================
        // EMAIL DOĞRULAMA
        // =========================================================

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return false;

            return password.Any(char.IsUpper) &&
                   password.Any(char.IsLower) &&
                   password.Any(char.IsDigit) &&
                   password.Any(ch => !char.IsLetterOrDigit(ch));
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr =
                    new System.Net.Mail.MailAddress(email);

                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // GİRİŞ YAPAN PERSONEL ID
        //
        // ARTIK SESSION DEĞİL CLAIM ÜZERİNDEN.
        // =========================================================

        private int? GetCurrentPersonelId()
        {
            var value =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (int.TryParse(
                value,
                out var personelId))
            {
                return personelId;
            }

            return null;
        }

        // =========================================================
        // SADECE PROFİL RESMİ CLAIM'İNİ GÜNCELLE
        // =========================================================

        private async Task UpdateProfileImageClaimAsync(
            string profilResmi)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var existingClaims =
                User.Claims.ToList();

            // Eski ProfilResmi claim'ini kaldır.
            existingClaims.RemoveAll(
                x => x.Type == "ProfilResmi");

            // Yeni claim'i ekle.
            existingClaims.Add(
                new Claim(
                    "ProfilResmi",
                    profilResmi ?? ""));

            var identity =
                new ClaimsIdentity(
                    existingClaims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            // Mevcut authentication özelliklerini koru.
            var authenticateResult =
                await HttpContext.AuthenticateAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var properties =
                authenticateResult.Properties
                ?? new AuthenticationProperties
                {
                    AllowRefresh = true
                };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                properties);
        }

        // =========================================================
        // KULLANICI KENDİ BİLGİLERİNİ GÜNCELLEDİĞİNDE
        // TÜM CLAIM'LERİ DB'DEN YENİDEN OLUŞTUR
        // =========================================================

        private async Task RefreshCurrentUserClaimsAsync(
            int personelId)
        {
            var personel =
                await _personelService
                    .GetPersonelWithDetailsAsync(personelId);

            if (personel == null)
            {
                return;
            }

            /*
             * Mevcut claim'leri koruyoruz.
             * Ardından kullanıcı bilgisi taşıyan claim'leri
             * güncelliyoruz.
             */

            var claims =
                User.Claims.ToList();

            ReplaceClaim(
                claims,
                ClaimTypes.NameIdentifier,
                personel.Id.ToString());

            ReplaceClaim(
                claims,
                ClaimTypes.Name,
                $"{personel.Ad} {personel.Soyad}");

            ReplaceClaim(
                claims,
                ClaimTypes.Email,
                personel.Email ?? "");

            ReplaceClaim(
                claims,
                "PersonelAdi",
                personel.Ad ?? "");

            ReplaceClaim(
                claims,
                "PersonelSoyadi",
                personel.Soyad ?? "");

            ReplaceClaim(
                claims,
                "SicilNo",
                personel.SicilNo ?? "");

            ReplaceClaim(
                claims,
                "ProfilResmi",
                personel.ProfilResmi ?? "");

            /*
             * Company / Branch / Gorev / Grup gibi navigation
             * property'ler GetPersonelWithDetailsAsync içinde
             * yükleniyorsa aşağıdaki claim'ler de yenilenir.
             */

            ReplaceClaim(
                claims,
                "CompanyName",
                personel.Company?.CompanyAdi ?? "");

            ReplaceClaim(
                claims,
                "BranchName",
                personel.Branch?.BranchAdi ?? "");

            ReplaceClaim(
                claims,
                "Gorev",
                personel.Gorev?.Ad ?? "");

            ReplaceClaim(
                claims,
                "Grup",
                personel.Grup?.Ad ?? "");

            ReplaceClaim(
                claims,
                "Cinsiyet",
                personel.Cinsiyet?.Ad ?? "");

            ReplaceClaim(
                claims,
                "CompanyId",
                personel.CompanyId?.ToString() ?? "");

            ReplaceClaim(
                claims,
                "BranchId",
                personel.BranchId?.ToString() ?? "");

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            var authenticateResult =
                await HttpContext.AuthenticateAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var properties =
                authenticateResult.Properties
                ?? new AuthenticationProperties
                {
                    AllowRefresh = true
                };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                properties);
        }

        // =========================================================
        // CLAIM DEĞİŞTİR
        // =========================================================

        private static void ReplaceClaim(
            List<Claim> claims,
            string claimType,
            string value)
        {
            claims.RemoveAll(
                x => x.Type == claimType);

            claims.Add(
                new Claim(
                    claimType,
                    value ?? ""));
        }
    }
}
