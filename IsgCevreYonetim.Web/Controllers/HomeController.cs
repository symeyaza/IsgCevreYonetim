using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Hubs;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IsgCevreYonetim.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrganizationService _organizationService;
        private readonly IYetkiService _yetkiService;
        private readonly IUserScopeService _userScopeService;
        private readonly IHubContext<OnlineUsersHub> _onlineUsersHub;

        public HomeController(
            ApplicationDbContext context,
            IOrganizationService organizationService,
            IYetkiService yetkiService,
            IUserScopeService userScopeService,
            IHubContext<OnlineUsersHub> onlineUsersHub)
        {
            _context = context;
            _organizationService = organizationService;
            _yetkiService = yetkiService;
            _userScopeService = userScopeService;
            _onlineUsersHub = onlineUsersHub;
        }

        // ==========================================================
        // LOGIN SAYFASI
        // ==========================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            // Cookie Authentication geçerliyse kullanıcı zaten giriş yapmıştır.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Dashboard));
            }

            return View("Login");
        }

        // ==========================================================
        // LOGIN
        // ==========================================================

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password,
            bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Email/Sicil No ve şifre alanları zorunludur.";

                ViewBag.Email = email;

                return View("Login");
            }

            email = email.Trim();

            // Email veya Sicil No ile kullanıcıyı bul
            var personel = await _context.Personeller
                .AsNoTracking()
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Gorev)
                .Include(p => p.Grup)
                .Include(p => p.Cinsiyet)
                .FirstOrDefaultAsync(p =>
                    (p.Email == email || p.SicilNo == email) &&
                    !p.IsDeleted &&
                    p.AktifMi);

            if (personel == null)
            {
                ViewBag.Error =
                    "Geçersiz email/sicil no veya şifre.";

                ViewBag.Email = email;

                return View("Login");
            }

            // Salt kontrolü
            if (string.IsNullOrWhiteSpace(personel.SifreSalt))
            {
                ViewBag.Error =
                    "Hesap bilgilerinde bir sorun var. " +
                    "Lütfen yöneticinizle iletişime geçin.";

                ViewBag.Email = email;

                return View("Login");
            }

            // Şifre kontrolü
            var passwordHash =
                HashPassword(password, personel.SifreSalt);

            if (passwordHash != personel.SifreHash)
            {
                ViewBag.Error =
                    "Geçersiz email/sicil no veya şifre.";

                ViewBag.Email = email;

                return View("Login");
            }

            // ======================================================
            // CLAIMS
            //
            // Kullanıcıya ait kalıcı oturum bilgileri Session yerine
            // authentication cookie içerisinde tutuluyor.
            // ======================================================

            var onlineSessionKey = Guid.NewGuid().ToString("N");
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    personel.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    $"{personel.Ad} {personel.Soyad}"),

                new Claim(
                    ClaimTypes.Email,
                    personel.Email ?? string.Empty),

                new Claim(
                    "PersonelAdi",
                    personel.Ad ?? string.Empty),

                new Claim(
                    "PersonelSoyadi",
                    personel.Soyad ?? string.Empty),

                new Claim(
                    "SicilNo",
                    personel.SicilNo ?? string.Empty),

                new Claim(
                    "ProfilResmi",
                    personel.ProfilResmi ?? string.Empty),

                new Claim(
                    "CompanyName",
                    personel.Company?.CompanyAdi ?? string.Empty),

                new Claim(
                    "BranchName",
                    personel.Branch?.BranchAdi ?? string.Empty),

                new Claim(
                    "Gorev",
                    personel.Gorev?.Ad ?? string.Empty),

                new Claim(
                    "Grup",
                    personel.Grup?.Ad ?? string.Empty),

                new Claim(
                    "Cinsiyet",
                    personel.Cinsiyet?.Ad ?? string.Empty),

                new Claim(
                    "CompanyId",
                    personel.CompanyId?.ToString() ?? string.Empty),

                new Claim(
                    "BranchId",
                    personel.BranchId?.ToString() ?? string.Empty),

                new Claim(
                    "YetkiId",
                    personel.YetkiId?.ToString() ?? string.Empty),

                new Claim(
                    "OnlineSessionKey",
                    onlineSessionKey)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var claimsPrincipal =
                new ClaimsPrincipal(claimsIdentity);

            // ======================================================
            // COOKIE AYARLARI
            // ======================================================

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true
            };

            // Beni hatırla seçilmişse browser kapansa da 14 gün sakla.
            if (rememberMe)
            {
                authProperties.ExpiresUtc =
                    DateTimeOffset.UtcNow.AddDays(14);
            }

            // Önceki kullanıcıdan kalmış operasyonel şirket/şube seçimini temizle.
            _userScopeService.ClearSelectedCompany();
            HttpContext.Session.Clear();

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            // Kaydı login isteği içinde oluşturuyoruz; yönlendirme/ilk sayfa JavaScript'ini beklemez.
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
MERGE [dbo].[OnlineKullaniciOturumlari] WITH (HOLDLOCK) AS target
USING (SELECT CAST({onlineSessionKey} AS nvarchar(64)) AS [SessionKey]) AS source
ON target.[SessionKey] = source.[SessionKey]
WHEN MATCHED THEN UPDATE SET
    [PersonelId] = {personel.Id},
    [CompanyId] = {personel.CompanyId ?? 0},
    [BranchId] = {personel.BranchId ?? 0},
    [LastSeenUtc] = {DateTime.UtcNow}
WHEN NOT MATCHED THEN INSERT ([SessionKey], [PersonelId], [CompanyId], [BranchId], [LastSeenUtc])
    VALUES (source.[SessionKey], {personel.Id}, {personel.CompanyId ?? 0}, {personel.BranchId ?? 0}, {DateTime.UtcNow});");
            await _onlineUsersHub.Clients.All.SendAsync("OnlineUsersChanged");

            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================================
        // DASHBOARD
        // ==========================================================

        [Authorize]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Dashboard()
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
            {
                await LogoutInternalAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.IsSystemAdmin = scope.IsSystemAdmin;
            ViewBag.CanSelectCompany = scope.CanSelectCompany;
            ViewBag.CanSelectBranch = scope.CanSelectBranch;
            ViewBag.SelectedCompanyId = scope.ActiveCompanyId.ToString();
            ViewBag.SelectedBranchId = scope.ActiveBranchId.ToString();
            ViewBag.ActiveCompanyName = scope.ActiveCompanyName;
            ViewBag.ActiveBranchName = scope.ActiveBranchName;

            if (scope.IsSystemAdmin)
            {
                // Süper Admin yeni oluşturulan şirketi, henüz şubesi olmasa bile
                // Dashboard şirket seçiminde hemen görebilmelidir.
                ViewBag.Companies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && !c.IsDeleted)
                    .OrderBy(c => c.CompanyAdi)
                    .ToListAsync();

                ViewBag.Branches = await _context.Branches
                    .AsNoTracking()
                    .Where(b =>
                        b.CompanyId == scope.ActiveCompanyId &&
                        b.IsActive &&
                        !b.IsDeleted)
                    .OrderBy(b => b.BranchAdi)
                    .ToListAsync();
            }
            else if (scope.CanSelectBranch)
            {
                ViewBag.Branches = await _context.Branches
                    .AsNoTracking()
                    .Where(b =>
                        b.CompanyId == scope.CompanyId &&
                        b.IsActive &&
                        !b.IsDeleted &&
                        _context.YetkiSayfalar.Any(ys =>
                            ys.BranchId == b.Id &&
                            ys.YetkiId == scope.YetkiId &&
                            ys.Goster &&
                            ys.IsActive &&
                            !ys.IsDeleted &&
                            ys.Sayfa.Url == "/Home/Dashboard" &&
                            ys.Sayfa.IsActive &&
                            !ys.Sayfa.IsDeleted))
                    .OrderBy(b => b.BranchAdi)
                    .ToListAsync();
            }
            else
            {
                _userScopeService.ClearSelectedBranch();
            }

            // Dashboard İSG istatistikleri her zaman seçili şubeye göre hesaplanır.
            // "Önemli kaza" için İş Kazası Raporu ile aynı varsayılan eşik kullanılır: >= 3 kayıp gün.
            var now = DateTime.Now;
            var today = now.Date;
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Pazartesi
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = new DateTime(now.Year, 1, 1);
            const int onemliKazaGun = 3;

            var branchAccidents = _context.IsKazalari
                .AsNoTracking()
                .Where(k => k.BranchId == scope.ActiveBranchId && !k.IsDeleted);

            var accidentStats = await branchAccidents
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Important = g.Count(k => (k.KayipGun ?? 0) >= onemliKazaGun),
                    LostDays = g.Sum(k => k.KayipGun ?? 0),
                    Daily = g.Count(k => k.KazaTarihi >= today),
                    DailyImportant = g.Count(k => k.KazaTarihi >= today && (k.KayipGun ?? 0) >= onemliKazaGun),
                    DailyLostDays = g.Where(k => k.KazaTarihi >= today).Sum(k => (int?)(k.KayipGun ?? 0)) ?? 0,
                    Weekly = g.Count(k => k.KazaTarihi >= weekStart),
                    WeeklyImportant = g.Count(k => k.KazaTarihi >= weekStart && (k.KayipGun ?? 0) >= onemliKazaGun),
                    WeeklyLostDays = g.Where(k => k.KazaTarihi >= weekStart).Sum(k => (int?)(k.KayipGun ?? 0)) ?? 0,
                    Monthly = g.Count(k => k.KazaTarihi >= monthStart),
                    MonthlyImportant = g.Count(k => k.KazaTarihi >= monthStart && (k.KayipGun ?? 0) >= onemliKazaGun),
                    MonthlyLostDays = g.Where(k => k.KazaTarihi >= monthStart).Sum(k => (int?)(k.KayipGun ?? 0)) ?? 0,
                    Yearly = g.Count(k => k.KazaTarihi >= yearStart),
                    YearlyImportant = g.Count(k => k.KazaTarihi >= yearStart && (k.KayipGun ?? 0) >= onemliKazaGun),
                    YearlyLostDays = g.Where(k => k.KazaTarihi >= yearStart).Sum(k => (int?)(k.KayipGun ?? 0)) ?? 0
                })
                .FirstOrDefaultAsync();

            ViewBag.TotalIsKazasi = accidentStats?.Total ?? 0;
            ViewBag.ImportantIsKazasi = accidentStats?.Important ?? 0;
            ViewBag.TotalLostDays = accidentStats?.LostDays ?? 0;
            ViewBag.DailyIsKazasi = accidentStats?.Daily ?? 0;
            ViewBag.WeeklyIsKazasi = accidentStats?.Weekly ?? 0;
            ViewBag.MonthlyIsKazasi = accidentStats?.Monthly ?? 0;
            ViewBag.YearlyIsKazasi = accidentStats?.Yearly ?? 0;
            ViewBag.DailyImportantIsKazasi = accidentStats?.DailyImportant ?? 0;
            ViewBag.WeeklyImportantIsKazasi = accidentStats?.WeeklyImportant ?? 0;
            ViewBag.MonthlyImportantIsKazasi = accidentStats?.MonthlyImportant ?? 0;
            ViewBag.YearlyImportantIsKazasi = accidentStats?.YearlyImportant ?? 0;
            ViewBag.DailyLostDays = accidentStats?.DailyLostDays ?? 0;
            ViewBag.WeeklyLostDays = accidentStats?.WeeklyLostDays ?? 0;
            ViewBag.MonthlyLostDays = accidentStats?.MonthlyLostDays ?? 0;
            ViewBag.YearlyLostDays = accidentStats?.YearlyLostDays ?? 0;
            ViewBag.OnemliKazaGun = onemliKazaGun;

            // Tehlikeli iş kartları seçili şubenin canlı kayıtlarından hesaplanır.
            // Yıllık sorgu tek seferde alınır; günlük/haftalık/aylık kırılımlar bellekte oluşturulur.
            var hazardRows = await _context.TehlikeliIsGunTehlikeSiniflari
                .AsNoTracking()
                .Where(x => x.Gun!.TehlikeliIs!.BranchId == scope.ActiveBranchId && x.Gun.Tarih >= yearStart)
                .Select(x => new { x.Gun!.Tarih, Sinif = x.TehlikeSinifi!.Ad })
                .ToListAsync();

            var hazardCategories = new[] { "hotwork", "height", "excavation", "confined" };
            var hazardPeriods = new[]
            {
                new { Name = "Daily", Start = today },
                new { Name = "Weekly", Start = weekStart },
                new { Name = "Monthly", Start = monthStart },
                new { Name = "Yearly", Start = yearStart }
            };
            foreach (var category in hazardCategories)
            foreach (var period in hazardPeriods)
                ViewData[$"{period.Name}{category}"] = hazardRows.Count(x =>
                    x.Tarih >= period.Start && MatchesHazardCategory(x.Sinif, category));

            // Atık türü miktarları seçili şubedeki kayıt tarihine göre kilogram olarak özetlenir.
            // Gruplama veritabanında yapılır; dashboard için tüm takip kayıtları belleğe alınmaz.
            ViewBag.AtikTurOzetleri = await _context.AtikTakipleri
                .AsNoTracking()
                .Where(x => x.BranchId == scope.ActiveBranchId && x.Tarih >= yearStart && x.Tarih < today.AddDays(1))
                .GroupBy(x => new { x.Atik!.AtikTuruId, x.Atik.AtikTuru!.AtikTurAdi })
                .Select(g => new IsgCevreYonetim.Web.Models.AtikTurDashboardOzet
                {
                    TurAdi = g.Key.AtikTurAdi,
                    Gunluk = g.Where(x => x.Tarih >= today).Sum(x => x.Miktar),
                    Haftalik = g.Where(x => x.Tarih >= weekStart).Sum(x => x.Miktar),
                    Aylik = g.Where(x => x.Tarih >= monthStart).Sum(x => x.Miktar),
                    Yillik = g.Sum(x => x.Miktar)
                })
                .OrderBy(x => x.TurAdi)
                .ToListAsync();

            // Gelir: satış bedeli; gider: bertaraf bedeli ve tüm nakliye bedelleri.
            // Bedelsiz işlemde birim bedel sıfırdır; varsa nakliye gider olarak hesaplanır.
            ViewBag.AtikFinansOzet = await _context.AtikTakipleri
                .AsNoTracking()
                .Where(x => x.BranchId == scope.ActiveBranchId && x.Tarih >= yearStart && x.Tarih < today.AddDays(1))
                .GroupBy(x => 1)
                .Select(g => new IsgCevreYonetim.Web.Models.AtikFinansDashboardOzet
                {
                    GunlukGelir = g.Sum(x => x.Tarih >= today && x.OdemeSatis == true ? x.Miktar / 1000m * x.BertarafBedeli : 0m),
                    GunlukGider = g.Sum(x => x.Tarih >= today ? (x.OdemeSatis == false ? x.Miktar / 1000m * x.BertarafBedeli : 0m) + x.NakliyeBedeli : 0m),
                    HaftalikGelir = g.Sum(x => x.Tarih >= weekStart && x.OdemeSatis == true ? x.Miktar / 1000m * x.BertarafBedeli : 0m),
                    HaftalikGider = g.Sum(x => x.Tarih >= weekStart ? (x.OdemeSatis == false ? x.Miktar / 1000m * x.BertarafBedeli : 0m) + x.NakliyeBedeli : 0m),
                    AylikGelir = g.Sum(x => x.Tarih >= monthStart && x.OdemeSatis == true ? x.Miktar / 1000m * x.BertarafBedeli : 0m),
                    AylikGider = g.Sum(x => x.Tarih >= monthStart ? (x.OdemeSatis == false ? x.Miktar / 1000m * x.BertarafBedeli : 0m) + x.NakliyeBedeli : 0m),
                    YillikGelir = g.Sum(x => x.OdemeSatis == true ? x.Miktar / 1000m * x.BertarafBedeli : 0m),
                    YillikGider = g.Sum(x => (x.OdemeSatis == false ? x.Miktar / 1000m * x.BertarafBedeli : 0m) + x.NakliyeBedeli)
                })
                .FirstOrDefaultAsync() ?? new IsgCevreYonetim.Web.Models.AtikFinansDashboardOzet();

            // Güncellemeler proje genelindeki geliştirme durumunu gösterir.
            // Pasif kayıtlar yönetim listesinde korunur ancak Dashboard'da gösterilmez.
            ViewBag.DashboardGuncellemeleri = await _context.DashboardGuncellemeleri
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.Kategori)
                .ThenBy(x => x.Durum)
                .ThenBy(x => x.Sira)
                .ThenBy(x => x.SayfaAdi)
                .ToListAsync();

            return View();
        }

        // ==========================================================
        // ŞİRKET SEÇ - SADECE SÜPER ADMIN
        // ==========================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCompany(int companyId)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
                return Unauthorized();

            if (!scope.IsSystemAdmin)
                return Forbid();

            var changed = await _userScopeService.TrySetActiveCompanyAsync(companyId);
            if (!changed)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Seçilen şirket bulunamadı veya aktif değil."
                });
            }

            return Ok(new { success = true });
        }

        // ==========================================================
        // ŞUBE SEÇ
        // ==========================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBranch(int branchId)
        {
            var scope = await _userScopeService.GetAsync();
            if (scope == null)
                return Unauthorized();

            if (!scope.CanSelectBranch)
                return Forbid();

            if (!scope.IsSystemAdmin)
            {
                var targetDashboardAllowed = await _yetkiService.KullaniciYetkiliMiAsync(
                    scope.PersonelId,
                    "/Home/Dashboard",
                    "Goster",
                    branchId);

                if (!targetDashboardAllowed)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        message = "Seçilen şubede Dashboard görüntüleme yetkiniz bulunmuyor. Şube değiştirilmedi."
                    });
                }
            }

            var changed = await _userScopeService.TrySetActiveBranchAsync(branchId);
            if (!changed)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Seçilen şube size ait şirket kapsamında değil veya aktif değil."
                });
            }

            return Ok(new { success = true });
        }

        // ==========================================================
        // ŞUBE SEÇİMİNİ TEMİZLE
        // ==========================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearBranch()
        {
            _userScopeService.ClearSelectedBranch();
            return Ok(new { success = true });
        }

        // ==========================================================
        // LOGOUT
        // ==========================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await LogoutInternalAsync();

            return RedirectToAction(nameof(Index));
        }

        // ==========================================================
        // ACCESS DENIED
        // ==========================================================

        [AllowAnonymous]
        public IActionResult AccessDenied(string? message = null)
        {
            ViewBag.AccessDeniedMessage = string.IsNullOrWhiteSpace(message)
                ? "Seçili şube ve size tanımlı yetki için bu sayfa/işlem izni verilmemiş."
                : message;
            return View();
        }

        // ==========================================================
        // YARDIMCI: LOGOUT
        // ==========================================================

        private async Task LogoutInternalAsync()
        {
            _userScopeService.ClearSelectedCompany();

            var sessionKey = User.FindFirstValue("OnlineSessionKey");
            if (string.IsNullOrWhiteSpace(sessionKey) &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var personelId))
            {
                sessionKey = $"legacy-{personelId}";
            }
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                var removed = await _context.OnlineKullaniciOturumlari
                    .Where(x => x.SessionKey == sessionKey)
                    .ExecuteDeleteAsync();
                if (removed > 0)
                    await _onlineUsersHub.Clients.All.SendAsync("OnlineUsersChanged");
            }

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();
        }

        private static bool MatchesHazardCategory(string? name, string category)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var value = name.Trim().ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            return category switch
            {
                "hotwork" => value.Contains("ATEŞ") || value.Contains("SICAK"),
                "height" => value.Contains("YÜKSEK"),
                "excavation" => value.Contains("KAZI"),
                "confined" => value.Contains("KAPALI"),
                _ => false
            };
        }

        // ==========================================================
        // ŞİFRE HASH
        // ==========================================================

        private static string HashPassword(
            string password,
            string salt)
        {
            if (string.IsNullOrWhiteSpace(salt))
            {
                throw new ArgumentException(
                    "Salt değeri boş olamaz.",
                    nameof(salt));
            }

            using var sha256 = SHA256.Create();

            var combined = password + salt;

            var bytes =
                Encoding.UTF8.GetBytes(combined);

            var hash =
                sha256.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}
