using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IsgCevreYonetim.Infrastructure.Seed
{
    public static class YetkiSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration)
        {
            var now = DateTime.Now;

            // =========================================================
            // SAYFALAR - URL bazlı UPSERT
            // =========================================================
            var pageDefinitions = new[]
            {
                new PageSeed("Dashboard", "/Home/Dashboard", "fa-chart-pie", 1),
                new PageSeed("Personel Listesi", "/Personel/Index", "fa-users", 2),
                new PageSeed("Şirketler", "/Organization/Companies", "fa-building", 3),
                new PageSeed("Şubeler", "/Organization/Branches", "fa-store", 4),
                new PageSeed("Departmanlar", "/Organization/Departments", "fa-sitemap", 5),
                new PageSeed("Birimler", "/Organization/Units", "fa-layer-group", 6),
                new PageSeed("İller", "/Reference/Iller", "fa-map-marker-alt", 7),
                new PageSeed("İlçeler", "/Reference/Ilceler", "fa-map", 8),
                new PageSeed("Cinsiyet", "/Reference/Cinsiyetler", "fa-venus-mars", 9),
                new PageSeed("Görevler", "/Reference/Gorevler", "fa-tasks", 10),
                new PageSeed("Gruplar", "/Reference/Gruplar", "fa-layer-group", 11),
                new PageSeed("Yetkilendirme", "/Yetki/Index", "fa-shield-alt", 12),
                new PageSeed("Yetkiler", "/Yetki/Yetkiler", "fa-key", 13),
                new PageSeed("Sayfalar", "/Yetki/Sayfalar", "fa-file", 14),
                new PageSeed("İş Kazaları", "/IsKazasi/Index", "fa-briefcase-medical", 15),
                new PageSeed("İş Kazası Kategori", "/IsKazasiArastirma/Kategoriler", "fa-folder-tree", 16),
                new PageSeed("İş Kazası Madde", "/IsKazasiArastirma/Maddeler", "fa-list-check", 17),
                new PageSeed("Ayrıntılı Kaza Araştırması", "/IsKazasiArastirma/Arastirma", "fa-magnifying-glass-chart", 18),
                new PageSeed("Düzeltici Faaliyetler", "/IsKazasiDuzelticiFaaliyet/Index", "fa-tools", 19),
                new PageSeed("Vardiyalar", "/Vardiya/Index", "fa-clock", 20),
                new PageSeed("İş Kazası Raporlama", "/IsKazasi/Rapor", "fa-chart-column", 21),
                new PageSeed("Çevre İşlemleri", "/Cevre/Index", "fa-leaf", 22),
                new PageSeed("Dashboard Güncellemeleri", "/DashboardGuncelleme/Index", "fa-arrows-rotate", 23),
                new PageSeed("İş Kazası Müdahale Şekilleri", "/MudahaleSekli/Index", "fa-kit-medical", 24),
                new PageSeed("Tehlikeli İşler", "/TehlikeliIs/Index", "fa-person-digging", 25),
                new PageSeed("Tehlike Sınıfları", "/TehlikeSinifi/Index", "fa-triangle-exclamation", 26),
                new PageSeed("İş Durumları", "/IsDurumu/Index", "fa-list-check", 27),
                new PageSeed("Personel Raporlama", "/Personel/Rapor", "fa-chart-column", 28),
                new PageSeed("Tehlikeli İş Raporlama", "/TehlikeliIs/Rapor", "fa-chart-column", 29),
                new PageSeed("Çevrimiçi Kullanıcılar", "/OnlineUsers/Index", "fa-user-clock", 30),
                new PageSeed("Atık Türleri", "/Cevre/Turler", "fa-list", 31),
                new PageSeed("Atık Firma Türleri", "/Cevre/FirmaTurleri", "fa-id-card", 32),
                new PageSeed("Atık Firmaları", "/Cevre/Firmalar", "fa-truck", 33),
                new PageSeed("Atıklar", "/Cevre/Atiklar", "fa-recycle", 34),
                new PageSeed("Atık Takibi", "/Cevre/Takip", "fa-clipboard-list", 35),
                new PageSeed("Taşeron Firmalar", "/Taseron/Index", "fa-helmet-safety", 36)
            };

            // Sayfa başına ayrı SELECT yerine mevcut sayfaları tek sorguda al.
            var pageUrls = pageDefinitions.Select(x => x.Url).ToArray();
            var existingPages = await context.Sayfalar
                .IgnoreQueryFilters()
                .Where(x => pageUrls.Contains(x.Url))
                .ToDictionaryAsync(x => x.Url);

            var newlyCreatedReportUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in pageDefinitions)
            {
                if (!existingPages.TryGetValue(def.Url, out var page))
                {
                    page = new Sayfa
                    {
                        Ad = def.Ad,
                        Url = def.Url,
                        Icon = def.Icon,
                        Sira = def.Sira,
                        CreatedDate = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    context.Sayfalar.Add(page);
                    if (def.Url is "/Personel/Rapor" or "/TehlikeliIs/Rapor") newlyCreatedReportUrls.Add(def.Url);
                }
                else
                {
                    page.Ad = def.Ad;
                    page.Icon = def.Icon;
                    page.Sira = def.Sira;
                    page.IsActive = true;
                    page.IsDeleted = false;
                    page.ParentId = null;
                    page.UpdatedDate = now;
                }
            }

            await context.SaveChangesAsync();

            // Yeni atık yönetimi ekranları ilk eklemede mevcut Çevre yetkilerini alır.
            // Sonraki düzenlemelerde bağımsız yetki satırları korunur.
            var wasteUrls = new[] { "/Cevre/Turler", "/Cevre/FirmaTurleri", "/Cevre/Firmalar", "/Cevre/Atiklar", "/Cevre/Takip" };
            var environmentPage = await context.Sayfalar.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Url == "/Cevre/Index");
            if (environmentPage != null)
            {
                var inherited = await context.YetkiSayfalar.IgnoreQueryFilters()
                    .AsNoTracking().Where(x => x.SayfaId == environmentPage.Id && !x.IsDeleted).ToListAsync();
                var wastePages = await context.Sayfalar.IgnoreQueryFilters().Where(x => wasteUrls.Contains(x.Url))
                    .Select(x => new { x.Id, x.Url }).ToListAsync();
                var pageIds = wastePages.Select(x => x.Id).ToArray();
                var existing = await context.YetkiSayfalar.IgnoreQueryFilters()
                    .Where(x => pageIds.Contains(x.SayfaId)).Select(x => new { x.SayfaId, x.YetkiId, x.BranchId }).ToListAsync();
                var keys = existing.Select(x => $"{x.SayfaId}:{x.YetkiId}:{x.BranchId}").ToHashSet();
                foreach (var page in wastePages)
                foreach (var permission in inherited)
                {
                    var key = $"{page.Id}:{permission.YetkiId}:{permission.BranchId}";
                    if (!keys.Add(key)) continue;
                    context.YetkiSayfalar.Add(new YetkiSayfa
                    {
                        SayfaId = page.Id, YetkiId = permission.YetkiId, BranchId = permission.BranchId,
                        Goster = permission.Goster, Ekle = permission.Ekle, Guncelle = permission.Guncelle,
                        Sil = permission.Sil, CreatedDate = now, IsActive = true, IsDeleted = false
                    });
                }
                await context.SaveChangesAsync();
            }

            // =========================================================
            // DÜZELTİCİ FAALİYETLER - İLK YETKİ GEÇİŞİ
            // =========================================================
            // Bu sayfa sonradan eklendiği için mevcut yetki profillerinde hiç
            // YetkiSayfa kaydı olmayabilir. Böyle bir durumda İş Kazaları
            // yetkisini yalnızca ilk kez miras alır. Sonrasında kullanıcı
            // Yetkilendirme ekranından bu sayfayı tamamen bağımsız yönetebilir.
            var isKazasiPage = await context.Sayfalar
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Url == "/IsKazasi/Index");
            var duzelticiPage = await context.Sayfalar
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Url == "/IsKazasiDuzelticiFaaliyet/Index");

            if (isKazasiPage != null && duzelticiPage != null)
            {
                var sourcePermissions = await context.YetkiSayfalar
                    .IgnoreQueryFilters()
                    .Where(x => x.SayfaId == isKazasiPage.Id && !x.IsDeleted)
                    .AsNoTracking()
                    .ToListAsync();

                var existingDuzelticiKeys = await context.YetkiSayfalar
                    .IgnoreQueryFilters()
                    .Where(x => x.SayfaId == duzelticiPage.Id && !x.IsDeleted)
                    .Select(x => new { x.YetkiId, x.BranchId })
                    .ToListAsync();
                var existingKeySet = existingDuzelticiKeys
                    .Select(x => $"{x.YetkiId}:{x.BranchId}")
                    .ToHashSet();

                foreach (var source in sourcePermissions)
                {
                    var key = $"{source.YetkiId}:{source.BranchId}";
                    if (existingKeySet.Contains(key))
                        continue;

                    context.YetkiSayfalar.Add(new YetkiSayfa
                    {
                        YetkiId = source.YetkiId,
                        BranchId = source.BranchId,
                        SayfaId = duzelticiPage.Id,
                        Goster = source.Goster,
                        Ekle = source.Ekle,
                        Guncelle = source.Guncelle,
                        Sil = source.Sil,
                        CreatedDate = now,
                        IsActive = true,
                        IsDeleted = false
                    });
                    existingKeySet.Add(key);
                }

                await context.SaveChangesAsync();
            }

            // Yeni rapor sayfaları ilk oluşturulduklarında liste görüntüleme yetkisini devralır.
            // Sonraki başlatmalarda mevcut rapor izinlerine dokunulmaz.
            foreach (var (sourceUrl, reportUrl) in new[]
            {
                ("/Personel/Index", "/Personel/Rapor"),
                ("/TehlikeliIs/Index", "/TehlikeliIs/Rapor")
            })
            {
                if (!newlyCreatedReportUrls.Contains(reportUrl)) continue;
                var sourcePage = existingPages.GetValueOrDefault(sourceUrl);
                var reportPage = await context.Sayfalar.FirstOrDefaultAsync(x => x.Url == reportUrl);
                if (sourcePage == null || reportPage == null) continue;
                var existingKeys = (await context.YetkiSayfalar.IgnoreQueryFilters()
                    .Where(x => x.SayfaId == reportPage.Id && !x.IsDeleted)
                    .Select(x => new { x.YetkiId, x.BranchId }).ToListAsync())
                    .Select(x => (x.YetkiId, x.BranchId)).ToHashSet();
                var sourceRows = await context.YetkiSayfalar.AsNoTracking()
                    .Where(x => x.SayfaId == sourcePage.Id && x.Goster && x.IsActive)
                    .ToListAsync();
                foreach (var row in sourceRows)
                {
                    if (!existingKeys.Add((row.YetkiId, row.BranchId))) continue;
                    context.YetkiSayfalar.Add(new YetkiSayfa
                    {
                        YetkiId = row.YetkiId, BranchId = row.BranchId, SayfaId = reportPage.Id,
                        Goster = true, Ekle = false, Guncelle = false, Sil = false,
                        IsActive = true, CreatedDate = now
                    });
                }
            }
            await context.SaveChangesAsync();

            // Eski modelde ayrı sayfa olarak seed edilmiş Personel/Ekle artık
            // /Personel/Index sayfasının Ekle yetkisini kullanıyor. Yetkilendirme
            // ekranında kullanılmayan ikinci bir satır görünmesin.
            var obsoletePersonelEkle = await context.Sayfalar
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Url == "/Personel/Ekle");

            if (obsoletePersonelEkle != null)
            {
                var obsoletePermissions = await context.YetkiSayfalar
                    .IgnoreQueryFilters()
                    .Where(x => x.SayfaId == obsoletePersonelEkle.Id)
                    .ToListAsync();
                context.YetkiSayfalar.RemoveRange(obsoletePermissions);
                context.Sayfalar.Remove(obsoletePersonelEkle);
                await context.SaveChangesAsync();
            }

            // =========================================================
            // YETKİ PROFİLLERİ - Ad bazlı UPSERT
            // Sistem yetki profilleri ad bazlı tekilleştirilerek oluşturulur.
            // =========================================================
            // Varsayılan yetkiler yalnız ilk kurulumda oluşturulur.
            // Kullanıcının sonradan sildiği "Tam Yetki / Yönetim Yetkisi / ..." kayıtları
            // uygulama restart olduğunda seed tarafından geri getirilmez.
            var hasAnyPermission = await context.Yetkiler
                .IgnoreQueryFilters()
                .AnyAsync(x => !x.IsDeleted);

            var permissionNames = hasAnyPermission
                ? new[] { "Süper Admin" }
                : new[]
                {
                    "Süper Admin",
                    "Tam Yetki",
                    "Yönetim Yetkisi",
                    "Personel Yetkisi",
                    "Görüntüleme Yetkisi"
                };

            // Yetki profillerini de 4 ayrı sorgu yerine tek sorguda getir.
            var existingPermissionRows = await context.Yetkiler
                .IgnoreQueryFilters()
                .Where(x => permissionNames.Contains(x.Ad))
                .OrderBy(x => x.Id)
                .ToListAsync();

            var existingPermissions = existingPermissionRows
                .GroupBy(x => x.Ad)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var name in permissionNames)
            {
                if (!existingPermissions.TryGetValue(name, out var permission))
                {
                    context.Yetkiler.Add(new Yetki
                    {
                        Ad = name,
                        CreatedDate = now,
                        IsActive = true,
                        IsDeleted = false
                    });
                }
                else
                {
                    permission.IsActive = true;
                    permission.IsDeleted = false;
                }
            }

            await context.SaveChangesAsync();

            // Sistemde tek bir "Süper Admin" yetki profili bulunur.
            var superAdminProfiles = await context.Yetkiler
                .IgnoreQueryFilters()
                .Where(y => y.Ad == "Süper Admin")
                .OrderBy(y => y.Id)
                .ToListAsync();

            var superAdminYetki = superAdminProfiles.First();
            superAdminYetki.IsActive = true;
            superAdminYetki.IsDeleted = false;

            foreach (var duplicate in superAdminProfiles.Skip(1))
            {
                var duplicatePersoneller = await context.Personeller
                    .IgnoreQueryFilters()
                    .Where(p => p.YetkiId == duplicate.Id)
                    .ToListAsync();
                foreach (var personel in duplicatePersoneller)
                    personel.YetkiId = superAdminYetki.Id;

                var duplicatePageRows = await context.YetkiSayfalar
                    .IgnoreQueryFilters()
                    .Where(x => x.YetkiId == duplicate.Id)
                    .ToListAsync();
                var duplicateBranchRows = await context.YetkiSubeler
                    .IgnoreQueryFilters()
                    .Where(x => x.YetkiId == duplicate.Id)
                    .ToListAsync();

                context.YetkiSayfalar.RemoveRange(duplicatePageRows);
                context.YetkiSubeler.RemoveRange(duplicateBranchRows);
                context.Yetkiler.Remove(duplicate);
            }

            await context.SaveChangesAsync();

            var tamYetki = await context.Yetkiler
                .FirstOrDefaultAsync(y => y.Ad == "Tam Yetki");

            // =========================================================
            // SEED / KURTARMA ADMİNİ
            // =========================================================
            // Yetkilendirme ekranında admin kendi profilini yanlışlıkla
            // değiştirmiş veya eski veriden farklı bir YetkiId kalmış olsa bile
            // sistemin yönetim erişimi kilitlenmesin.
            //
            // Bu yalnız Seed:SuperAdmin:SicilNo ile tanımlanan başlangıç
            // yöneticisi için uygulanır. Normal personellerin YetkiId değerine
            // dokunulmaz.
            var superAdminSicilNo =
                configuration["Seed:SuperAdmin:SicilNo"] ?? "SYS0001";

            var seededAdmin = await context.Personeller
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.SicilNo == superAdminSicilNo);

            if (seededAdmin != null)
            {
                // Konfigürasyondaki sistem yöneticisi daima tek Süper Admin profiline bağlıdır.
                // Ayrıca hiçbir şirkete/şubeye/departmana/birime kalıcı olarak bağlı değildir.
                // Çalışma kapsamını yalnız Dashboard'taki Şirket + Şube seçimi belirler.
                seededAdmin.YetkiId = superAdminYetki.Id;
                seededAdmin.CompanyId = null;
                seededAdmin.BranchId = null;
                seededAdmin.DepartmentId = null;
                seededAdmin.UnitId = null;
                seededAdmin.UpdatedDate = now;
            }

            // Süper Admin yetkisini aynı anda yalnız tek kullanıcı taşıyabilir.
            // Eski veriden bu profile bağlı başka kullanıcılar varsa Tam Yetki'ye alınır.
            var existingSuperAdminPersonelIds = await context.Personeller
                .IgnoreQueryFilters()
                .Where(p => p.YetkiId == superAdminYetki.Id && !p.IsDeleted)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var keeperPersonelId = seededAdmin?.Id ?? existingSuperAdminPersonelIds.FirstOrDefault();
            var otherSuperAdmins = await context.Personeller
                .IgnoreQueryFilters()
                .Where(p =>
                    p.YetkiId == superAdminYetki.Id &&
                    p.Id != keeperPersonelId)
                .ToListAsync();

            foreach (var personel in otherSuperAdmins)
            {
                // Tam Yetki kullanıcı tarafından silinmiş olabilir; silinen yetkiyi seed ile
                // yeniden oluşturmayız. Uygun profil yoksa YetkiId boş bırakılır.
                personel.YetkiId = tamYetki?.Id;
                personel.UpdatedDate = now;
            }

            if (seededAdmin != null || otherSuperAdmins.Count > 0)
                await context.SaveChangesAsync();

            // =========================================================
            // İLK KURULUM TAM YETKİ VARSAYILANLARI
            // =========================================================
            // Tam Yetki için otomatik CRUD yalnız veritabanında bu profile ait
            // hiçbir YetkiSayfa kaydı yoksa, yani ilk kurulumda oluşturulur.
            // Daha sonraki yeni şube/sayfa kayıtlarında otomatik yetki VERİLMEZ.
            // Böylece çalışma zamanı ve sonraki restartlarda tek gerçek kaynak
            // Yetkilendirme ekranı olarak kalır.
            if (tamYetki != null)
            {
                var hasAnyTamYetkiPermission = await context.YetkiSayfalar
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.YetkiId == tamYetki.Id);

                if (!hasAnyTamYetkiPermission)
                {
                    var branches = await context.Branches
                        .IgnoreQueryFilters()
                        .Where(b => !b.IsDeleted && b.IsActive)
                        .ToListAsync();

                    var pages = await context.Sayfalar
                        .Where(p => p.IsActive && !p.IsDeleted)
                        .ToListAsync();

                    foreach (var branch in branches)
                    {
                        context.YetkiSubeler.Add(new YetkiSube
                        {
                            YetkiId = tamYetki.Id,
                            BranchId = branch.Id,
                            LokasyonSecebilir = true,
                            CreatedDate = now,
                            IsActive = true,
                            IsDeleted = false
                        });

                        foreach (var page in pages)
                        {
                            context.YetkiSayfalar.Add(new YetkiSayfa
                            {
                                YetkiId = tamYetki.Id,
                                BranchId = branch.Id,
                                SayfaId = page.Id,
                                Ekle = true,
                                Guncelle = true,
                                Sil = true,
                                Goster = true,
                                CreatedDate = now,
                                IsActive = true,
                                IsDeleted = false
                            });
                        }
                    }

                    await context.SaveChangesAsync();
                }
            }
        }

        private sealed record PageSeed(
            string Ad,
            string Url,
            string Icon,
            int Sira);
    }
}
