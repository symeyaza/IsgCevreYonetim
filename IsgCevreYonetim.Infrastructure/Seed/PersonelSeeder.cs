using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace IsgCevreYonetim.Infrastructure.Seed
{
    public static class PersonelSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            // Referans tabloları mevcut personel sayısından bağımsız olarak kontrol edilir.
            // Önceki "Personel varsa return" yaklaşımı, mevcut veritabanlarında SYS0001
            // Süper Admin hesabının oluşturulmasını / onarılmasını engelliyordu.
            await SeedReferenceTablesAsync(context);

            var now = DateTime.Now;
            var adminConfig = configuration.GetSection("Seed:SuperAdmin");

            var sicilNo = (adminConfig["SicilNo"] ?? "SYS0001").Trim();
            var email = (adminConfig["Email"] ?? "admin@localhost.com").Trim();
            var password = adminConfig["Password"];
            var firstName = adminConfig["FirstName"] ?? "Sistem";
            var lastName = adminConfig["LastName"] ?? "Yöneticisi";

            // Bu proje için Süper Admin başlangıç parolası konfigürasyondan yönetilir.
            // true olduğunda her uygulama açılışında SYS0001 parolası konfigürasyondaki
            // değere eşitlenir. Böylece mevcut veritabanında eski/farklı hash kalmış olsa
            // bile sistem sahibi hesabına erişebilir. İstenirse appsettings'te false yapılarak
            // daha sonra uygulama içinden değiştirilen parolanın korunması sağlanabilir.
            var resetPasswordOnStartup =
                bool.TryParse(adminConfig["ResetPasswordOnStartup"], out var resetPassword) &&
                resetPassword;

            if (string.IsNullOrWhiteSpace(sicilNo))
                sicilNo = "SYS0001";



            // =========================================================
            // 1. TEK SÜPER ADMIN YETKİSİNİ BUL / OLUŞTUR
            // =========================================================
            var superAdminYetki = await context.Yetkiler
                .IgnoreQueryFilters()
                .Where(y => y.Ad == "Süper Admin")
                .OrderBy(y => y.Id)
                .FirstOrDefaultAsync();

            if (superAdminYetki == null)
            {
                superAdminYetki = new Yetki
                {
                    Ad = "Süper Admin",
                    CreatedDate = now,
                    IsActive = true,
                    IsDeleted = false
                };

                await context.Yetkiler.AddAsync(superAdminYetki);
                await context.SaveChangesAsync();
            }
            else
            {
                superAdminYetki.Ad = "Süper Admin";
                superAdminYetki.IsActive = true;
                superAdminYetki.IsDeleted = false;
            }

            // =========================================================
            // 2. SÜPER ADMIN PERSONELİNİ BUL / OLUŞTUR / ONAR
            // =========================================================
            var admin = await context.Personeller
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.SicilNo == sicilNo);

            var gorev = await context.Gorevler
                .FirstOrDefaultAsync(g => g.Ad == "Yönetici");

            var grup = await context.Gruplar
                .FirstOrDefaultAsync(g => g.Ad == "Yönetim");

            var erkek = await context.Cinsiyetler
                .FirstOrDefaultAsync(c => c.Ad == "Erkek");

            if (admin == null)
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("İlk yönetici hesabı için Seed:SuperAdmin:Password ortam değişkeni gereklidir.");
                var salt = GenerateSalt();

                admin = new Personel
                {
                    Ad = firstName,
                    Soyad = lastName,
                    Email = email,
                    SicilNo = sicilNo,

                    SifreSalt = salt,
                    SifreHash = HashPassword(password!, salt),

                    CinsiyetId = erkek?.Id,
                    IseGirisTarihi = now,

                    // Süper Admin organizasyondan tamamen bağımsızdır.
                    // Çalışacağı Şirket + Şube yalnız Dashboard seçiminden gelir.
                    CompanyId = null,
                    BranchId = null,
                    DepartmentId = null,
                    UnitId = null,

                    GorevId = gorev?.Id,
                    GrupId = grup?.Id,
                    YetkiId = superAdminYetki.Id,

                    AktifMi = true,
                    EmailDogrulandi = true,
                    BasarisizGirisSayisi = 0,
                    KilitlenmeTarihi = null,

                    CreatedDate = now,
                    UpdatedDate = null,
                    IsActive = true,
                    IsDeleted = false
                };

                await context.Personeller.AddAsync(admin);
            }
            else
            {
                // Hesap daha önce oluşturulmuş olsa bile Süper Admin kimliği ve erişim
                // kuralları her başlangıçta onarılır. Böylece normal bir şirkete/şubeye
                // yanlışlıkla bağlanması veya pasife alınması sistem sahibini kilitlemez.
                admin.Ad = firstName;
                admin.Soyad = lastName;
                admin.Email = email;
                admin.SicilNo = sicilNo;

                admin.CompanyId = null;
                admin.BranchId = null;
                admin.DepartmentId = null;
                admin.UnitId = null;

                admin.YetkiId = superAdminYetki.Id;
                admin.AktifMi = true;
                admin.EmailDogrulandi = true;
                admin.IsActive = true;
                admin.IsDeleted = false;
                admin.BasarisizGirisSayisi = 0;
                admin.KilitlenmeTarihi = null;
                admin.UpdatedDate = now;

                if (admin.GorevId == null)
                    admin.GorevId = gorev?.Id;

                if (admin.GrupId == null)
                    admin.GrupId = grup?.Id;

                if (admin.CinsiyetId == null)
                    admin.CinsiyetId = erkek?.Id;

                // Parola kaydı eksik/bozuksa her durumda onar.
                // Ayrıca ResetPasswordOnStartup=true ise mevcut parola da konfigürasyondaki
                // başlangıç parolasına sıfırlanır.
                if (resetPasswordOnStartup ||
                    string.IsNullOrWhiteSpace(admin.SifreSalt) ||
                    string.IsNullOrWhiteSpace(admin.SifreHash))
                {
                    if (string.IsNullOrWhiteSpace(password))
                        throw new InvalidOperationException("Yönetici parolasını sıfırlamak veya onarmak için Seed:SuperAdmin:Password gereklidir.");
                    var salt = GenerateSalt();
                    admin.SifreSalt = salt;
                    admin.SifreHash = HashPassword(password!, salt);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedReferenceTablesAsync(
            ApplicationDbContext context)
        {
            // =========================================================
            // CİNSİYETLER
            // =========================================================
            if (!await context.Cinsiyetler.AnyAsync())
            {
                var cinsiyetler = new[]
                {
                    new Cinsiyet
                    {
                        Ad = "Erkek",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Cinsiyet
                    {
                        Ad = "Kadın",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Cinsiyet
                    {
                        Ad = "Belirtilmedi",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    }
                };

                await context.Cinsiyetler
                    .AddRangeAsync(cinsiyetler);

                await context.SaveChangesAsync();
            }

            // =========================================================
            // GÖREVLER
            // =========================================================
            if (!await context.Gorevler.AnyAsync())
            {
                var gorevler = new[]
                {
                    new Gorev
                    {
                        Ad = "Yönetici",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Gorev
                    {
                        Ad = "Uzman",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Gorev
                    {
                        Ad = "Personel",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Gorev
                    {
                        Ad = "Stajyer",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    }
                };

                await context.Gorevler
                    .AddRangeAsync(gorevler);

                await context.SaveChangesAsync();
            }

            // =========================================================
            // GRUPLAR
            // =========================================================
            if (!await context.Gruplar.AnyAsync())
            {
                var gruplar = new[]
                {
                    new Grup
                    {
                        Ad = "Yönetim",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Grup
                    {
                        Ad = "İSG",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Grup
                    {
                        Ad = "Çevre",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    },

                    new Grup
                    {
                        Ad = "Personel",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    }
                };

                await context.Gruplar
                    .AddRangeAsync(gruplar);

                await context.SaveChangesAsync();
            }

            // =========================================================
            // İLLER
            // =========================================================
            if (!await context.Iller.AnyAsync())
            {
                var iller = new[]
                {
                    new Il { Kod = "01", Ad = "Adana" },
                    new Il { Kod = "02", Ad = "Adıyaman" },
                    new Il { Kod = "03", Ad = "Afyonkarahisar" },
                    new Il { Kod = "04", Ad = "Ağrı" },
                    new Il { Kod = "05", Ad = "Amasya" },
                    new Il { Kod = "06", Ad = "Ankara" },
                    new Il { Kod = "07", Ad = "Antalya" },
                    new Il { Kod = "08", Ad = "Artvin" },
                    new Il { Kod = "09", Ad = "Aydın" },
                    new Il { Kod = "10", Ad = "Balıkesir" },
                    new Il { Kod = "11", Ad = "Bilecik" },
                    new Il { Kod = "12", Ad = "Bingöl" },
                    new Il { Kod = "13", Ad = "Bitlis" },
                    new Il { Kod = "14", Ad = "Bolu" },
                    new Il { Kod = "15", Ad = "Burdur" },
                    new Il { Kod = "16", Ad = "Bursa" },
                    new Il { Kod = "17", Ad = "Çanakkale" },
                    new Il { Kod = "18", Ad = "Çankırı" },
                    new Il { Kod = "19", Ad = "Çorum" },
                    new Il { Kod = "20", Ad = "Denizli" },
                    new Il { Kod = "21", Ad = "Diyarbakır" },
                    new Il { Kod = "22", Ad = "Edirne" },
                    new Il { Kod = "23", Ad = "Elazığ" },
                    new Il { Kod = "24", Ad = "Erzincan" },
                    new Il { Kod = "25", Ad = "Erzurum" },
                    new Il { Kod = "26", Ad = "Eskişehir" },
                    new Il { Kod = "27", Ad = "Gaziantep" },
                    new Il { Kod = "28", Ad = "Giresun" },
                    new Il { Kod = "29", Ad = "Gümüşhane" },
                    new Il { Kod = "30", Ad = "Hakkari" },
                    new Il { Kod = "31", Ad = "Hatay" },
                    new Il { Kod = "32", Ad = "Isparta" },
                    new Il { Kod = "33", Ad = "Mersin" },
                    new Il { Kod = "34", Ad = "İstanbul" },
                    new Il { Kod = "35", Ad = "İzmir" },
                    new Il { Kod = "36", Ad = "Kars" },
                    new Il { Kod = "37", Ad = "Kastamonu" },
                    new Il { Kod = "38", Ad = "Kayseri" },
                    new Il { Kod = "39", Ad = "Kırklareli" },
                    new Il { Kod = "40", Ad = "Kırşehir" },
                    new Il { Kod = "41", Ad = "Kocaeli" },
                    new Il { Kod = "42", Ad = "Konya" },
                    new Il { Kod = "43", Ad = "Kütahya" },
                    new Il { Kod = "44", Ad = "Malatya" },
                    new Il { Kod = "45", Ad = "Manisa" },
                    new Il { Kod = "46", Ad = "Kahramanmaraş" },
                    new Il { Kod = "47", Ad = "Mardin" },
                    new Il { Kod = "48", Ad = "Muğla" },
                    new Il { Kod = "49", Ad = "Muş" },
                    new Il { Kod = "50", Ad = "Nevşehir" },
                    new Il { Kod = "51", Ad = "Niğde" },
                    new Il { Kod = "52", Ad = "Ordu" },
                    new Il { Kod = "53", Ad = "Rize" },
                    new Il { Kod = "54", Ad = "Sakarya" },
                    new Il { Kod = "55", Ad = "Samsun" },
                    new Il { Kod = "56", Ad = "Siirt" },
                    new Il { Kod = "57", Ad = "Sinop" },
                    new Il { Kod = "58", Ad = "Sivas" },
                    new Il { Kod = "59", Ad = "Tekirdağ" },
                    new Il { Kod = "60", Ad = "Tokat" },
                    new Il { Kod = "61", Ad = "Trabzon" },
                    new Il { Kod = "62", Ad = "Tunceli" },
                    new Il { Kod = "63", Ad = "Şanlıurfa" },
                    new Il { Kod = "64", Ad = "Uşak" },
                    new Il { Kod = "65", Ad = "Van" },
                    new Il { Kod = "66", Ad = "Yozgat" },
                    new Il { Kod = "67", Ad = "Zonguldak" },
                    new Il { Kod = "68", Ad = "Aksaray" },
                    new Il { Kod = "69", Ad = "Bayburt" },
                    new Il { Kod = "70", Ad = "Karaman" },
                    new Il { Kod = "71", Ad = "Kırıkkale" },
                    new Il { Kod = "72", Ad = "Batman" },
                    new Il { Kod = "73", Ad = "Şırnak" },
                    new Il { Kod = "74", Ad = "Bartın" },
                    new Il { Kod = "75", Ad = "Ardahan" },
                    new Il { Kod = "76", Ad = "Iğdır" },
                    new Il { Kod = "77", Ad = "Yalova" },
                    new Il { Kod = "78", Ad = "Karabük" },
                    new Il { Kod = "79", Ad = "Kilis" },
                    new Il { Kod = "80", Ad = "Osmaniye" },
                    new Il { Kod = "81", Ad = "Düzce" }
                };

                foreach (var il in iller)
                {
                    il.CreatedDate = DateTime.Now;
                    il.IsActive = true;
                    il.IsDeleted = false;
                }

                await context.Iller.AddRangeAsync(iller);
                await context.SaveChangesAsync();
            }

            // =========================================================
            // İSTANBUL İLÇELERİ
            // =========================================================
            if (!await context.Ilceler.AnyAsync())
            {
                var istanbul = await context.Iller
                    .FirstOrDefaultAsync(i => i.Kod == "34");

                if (istanbul != null)
                {
                    var ilceler = new[]
                    {
                        "Adalar",
                        "Arnavutköy",
                        "Ataşehir",
                        "Avcılar",
                        "Bağcılar",
                        "Bahçelievler",
                        "Bakırköy",
                        "Başakşehir",
                        "Bayrampaşa",
                        "Beşiktaş",
                        "Beykoz",
                        "Beylikdüzü",
                        "Beyoğlu",
                        "Büyükçekmece",
                        "Çatalca",
                        "Çekmeköy",
                        "Esenler",
                        "Esenyurt",
                        "Eyüpsultan",
                        "Fatih",
                        "Gaziosmanpaşa",
                        "Güngören",
                        "Kadıköy",
                        "Kağıthane",
                        "Kartal",
                        "Küçükçekmece",
                        "Maltepe",
                        "Pendik",
                        "Sancaktepe",
                        "Sarıyer",
                        "Silivri",
                        "Sultanbeyli",
                        "Sultangazi",
                        "Şile",
                        "Şişli",
                        "Tuzla",
                        "Ümraniye",
                        "Üsküdar",
                        "Zeytinburnu"
                    };

                    var ilceList = ilceler.Select(ilce => new Ilce
                    {
                        Ad = ilce,
                        IlId = istanbul.Id,

                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    });

                    await context.Ilceler
                        .AddRangeAsync(ilceList);

                    await context.SaveChangesAsync();
                }
            }
        }

        private static string GenerateSalt()
        {
            var saltBytes = new byte[32];

            using var rng =
                RandomNumberGenerator.Create();

            rng.GetBytes(saltBytes);

            return Convert.ToBase64String(saltBytes);
        }

        private static string HashPassword(
            string password,
            string salt)
        {
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