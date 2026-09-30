using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class PersonelService : IPersonelService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMemoryCache _cache;
        private static int? _superAdminYetkiId;
        private static readonly SemaphoreSlim SuperAdminYetkiLock = new(1, 1);

        public PersonelService(
            ApplicationDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment webHostEnvironment,
            IMemoryCache cache)
        {
            _context = context;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
            _cache = cache;
        }

        public async Task<Personel> GetByIdAsync(int id)
        {
            // CRUD kapsam kontrollerinde navigation verilerine ihtiyaç yok.
            // Ağır Include zinciri yerine tek Personel satırını getirir.
            return await _context.Personeller
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<Personel> GetByEmailAsync(string email)
        {
            return await _context.Personeller
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Gorev)
                .Include(p => p.Grup)
                .Include(p => p.Cinsiyet)
                .Include(p => p.Yetki)
                .FirstOrDefaultAsync(p =>
                    p.Email == email &&
                    !p.IsDeleted &&
                    p.AktifMi);
        }

        public async Task<Personel> GetBySicilNoAsync(string sicilNo)
        {
            return await _context.Personeller
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Gorev)
                .Include(p => p.Grup)
                .Include(p => p.Cinsiyet)
                .Include(p => p.Yetki)
                .FirstOrDefaultAsync(p =>
                    p.SicilNo == sicilNo &&
                    !p.IsDeleted &&
                    p.AktifMi);
        }

        public async Task<IEnumerable<Personel>> GetAllAsync()
        {
            return await _context.Personeller
                .AsNoTracking()
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Department)
                .Include(p => p.Unit)
                .Include(p => p.Gorev)
                .Include(p => p.Grup)
                .Include(p => p.Cinsiyet)
                .Include(p => p.Yetki)
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Ad)
                .ThenBy(p => p.Soyad)
                .ToListAsync();
        }

        public async Task<IEnumerable<Personel>> GetByCompanyIdAsync(int companyId)
        {
            return await _context.Personeller
                .AsNoTracking()
                .Include(p => p.Branch)
                .Include(p => p.Department)
                .Include(p => p.Yetki)
                .Where(p => p.CompanyId == companyId && !p.IsDeleted)
                .OrderBy(p => p.Ad)
                .ThenBy(p => p.Soyad)
                .ToListAsync();
        }

        public async Task<IEnumerable<Personel>> GetByBranchIdAsync(int branchId)
        {
            return await _context.Personeller
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.Unit)
                .Include(p => p.Yetki)
                .Where(p => p.BranchId == branchId && !p.IsDeleted)
                .OrderBy(p => p.Ad)
                .ThenBy(p => p.Soyad)
                .ToListAsync();
        }

        public async Task<Personel> GetPersonelWithDetailsAsync(int id)
        {
            return await _context.Personeller
                .AsNoTracking()
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Department)
                .Include(p => p.Unit)
                .Include(p => p.Gorev)
                .Include(p => p.Grup)
                .Include(p => p.Cinsiyet)
                .Include(p => p.Yetki)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<bool> ValidatePasswordAsync(Personel personel, string password)
        {
            if (personel == null || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(personel.SifreSalt))
                return false;

            var hash = HashPassword(password, personel.SifreSalt);
            return hash == personel.SifreHash;
        }

        public async Task<bool> ChangePasswordAsync(int personelId, string oldPassword, string newPassword)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel == null)
                return false;

            if (!await ValidatePasswordAsync(personel, oldPassword))
                return false;

            var salt = GenerateSalt();
            personel.SifreHash = HashPassword(newPassword, salt);
            personel.SifreSalt = salt;
            personel.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ResetPasswordAsync(int personelId, string newPassword)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel == null)
                return false;

            var salt = GenerateSalt();
            personel.SifreHash = HashPassword(newPassword, salt);
            personel.SifreSalt = salt;
            personel.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsEmailExistAsync(string email, int? excludeId = null)
        {
            var query = _context.Personeller.Where(p => p.Email == email && !p.IsDeleted);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> IsSicilNoExistAsync(string sicilNo, int? excludeId = null)
        {
            var query = _context.Personeller.Where(p => p.SicilNo == sicilNo && !p.IsDeleted);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<(bool EmailExists, bool SicilNoExists)> CheckUniqueFieldsAsync(
            string email, string sicilNo, int? excludeId = null)
        {
            var query = _context.Personeller
                .AsNoTracking()
                .Where(p => !p.IsDeleted && (p.Email == email || p.SicilNo == sicilNo));

            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);

            // Email ve sicil kontrolünü iki ayrı round-trip yerine tek SQL'de yap.
            var matches = await query
                .Select(p => new { p.Email, p.SicilNo })
                .Take(2)
                .ToListAsync();

            return (
                matches.Any(x => x.Email == email),
                matches.Any(x => x.SicilNo == sicilNo));
        }

        public async Task<bool> ValidateOrganizationSelectionAsync(
            int companyId, int branchId, int? departmentId, int? unitId)
        {
            if (unitId.HasValue && !departmentId.HasValue)
                return false;

            var cacheKey = $"personel:orgvalid:{companyId}:{branchId}:{departmentId?.ToString() ?? "-"}:{unitId?.ToString() ?? "-"}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(2));
                var branchQuery = _context.Branches.AsNoTracking()
                    .Where(b => b.Id == branchId && b.CompanyId == companyId && !b.IsDeleted && b.IsActive);

                if (!departmentId.HasValue)
                    return await branchQuery.AnyAsync();

                var departmentIdValue = departmentId.Value;
                return await branchQuery.AnyAsync(b => b.Departments.Any(d =>
                    d.Id == departmentIdValue && !d.IsDeleted && d.IsActive &&
                    (!unitId.HasValue || d.Units.Any(u => u.Id == unitId.Value && !u.IsDeleted && u.IsActive))));
            });
        }

        public async Task<Personel> CreateAsync(Personel personel, string password)
        {
            if (string.IsNullOrEmpty(personel.SicilNo))
            {
                throw new Exception("Sicil No zorunludur!");
            }

            await EnsureSingleSuperAdminAssignmentAsync(personel.YetkiId, null);

            var salt = GenerateSalt();
            personel.SifreHash = HashPassword(password, salt);
            personel.SifreSalt = salt;
            personel.CreatedDate = DateTime.Now;
            personel.IsDeleted = false;

            await _context.Personeller.AddAsync(personel);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw TranslatePersonelUniqueException(ex);
            }
            return personel;
        }

        public async Task UpdateAsync(Personel personel, string? newPassword = null)
        {
            var existing = await _context.Personeller.FindAsync(personel.Id);
            if (existing == null || existing.IsDeleted)
                throw new Exception("Personel bulunamadı!");

            var configuredSuperAdminSicilNo = _configuration["Seed:SuperAdmin:SicilNo"] ?? "SYS0001";
            if (string.Equals(existing.SicilNo, configuredSuperAdminSicilNo, StringComparison.OrdinalIgnoreCase))
            {
                var isTargetSuperAdmin = personel.YetkiId.HasValue && await _context.Yetkiler
                    .AsNoTracking()
                    .AnyAsync(y =>
                        y.Id == personel.YetkiId.Value &&
                        y.Ad == "Süper Admin" &&
                        y.IsActive &&
                        !y.IsDeleted);

                if (!isTargetSuperAdmin)
                    throw new Exception("Sistem Süper Admin kullanıcısının yetkisi değiştirilemez.");

                // Sistem yöneticisini konfigürasyondaki sabit sicil numarasından koparma.
                personel.SicilNo = existing.SicilNo;
            }

            await EnsureSingleSuperAdminAssignmentAsync(personel.YetkiId, personel.Id);

            // Formdan gelen ikinci entity instance'ını attach etmek yerine mevcut tracked
            // kayıt üzerinde yalnız düzenlenebilir alanları güncelle. Login/audit bilgileri korunur.
            existing.Ad = personel.Ad;
            existing.Soyad = personel.Soyad;
            existing.Email = personel.Email;
            existing.SicilNo = personel.SicilNo;
            existing.Telefon = personel.Telefon;
            existing.CepTelefonu = personel.CepTelefonu;
            existing.CinsiyetId = personel.CinsiyetId;
            existing.DogumTarihi = personel.DogumTarihi;
            existing.IseGirisTarihi = personel.IseGirisTarihi;
            existing.EmailDogrulandi = personel.EmailDogrulandi;
            existing.AktifMi = personel.AktifMi;
            existing.IsActive = personel.IsActive;
            existing.ProfilResmi = string.IsNullOrWhiteSpace(personel.ProfilResmi)
                ? existing.ProfilResmi
                : personel.ProfilResmi;
            existing.GorevId = personel.GorevId;
            existing.GrupId = personel.GrupId;
            existing.CompanyId = personel.CompanyId;
            existing.BranchId = personel.BranchId;
            existing.DepartmentId = personel.DepartmentId;
            existing.UnitId = personel.UnitId;
            existing.YetkiId = personel.YetkiId;

            // Şifre yalnızca kullanıcı Düzenle formunda yeni bir şifre girdiyse değiştirilir.
            // Boş bırakıldığında mevcut hash/salt aynen korunur.
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var salt = GenerateSalt();
                existing.SifreHash = HashPassword(newPassword, salt);
                existing.SifreSalt = salt;
            }

            existing.UpdatedDate = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw TranslatePersonelUniqueException(ex);
            }
        }

        public async Task DeleteAsync(int id)
        {
            // Controller aynı personeli daha önce yüklediyse FindAsync change tracker'dan
            // döner; tekrar SELECT çalıştırmaz.
            var personel = await _context.Personeller.FindAsync(id);
            if (personel == null || personel.IsDeleted)
                return;

            var configuredSuperAdminSicilNo = _configuration["Seed:SuperAdmin:SicilNo"] ?? "SYS0001";
            if (string.Equals(personel.SicilNo, configuredSuperAdminSicilNo, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Sistem Süper Admin kullanıcısı silinemez.");

            var hasAccidents = await _context.IsKazalari
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.PersonelId == id);
            if (hasAccidents)
                throw new Exception("Bu personele bağlı iş kazası kayıtları bulunuyor. Önce iş kazası kayıtlarını silmelisiniz.");

            var profilePath = personel.ProfilResmi;

            _context.Personeller.Remove(personel);
            await _context.SaveChangesAsync();

            // DB silme başarılı olduktan sonra fiziksel dosyayı temizle.
            if (!string.IsNullOrWhiteSpace(profilePath))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, profilePath.TrimStart('/'));
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        public async Task<bool> LoginAsync(string emailOrSicil, string password, string ipAddress, string userAgent)
        {
            var personel = await _context.Personeller
                .Include(p => p.Company)
                .Include(p => p.Branch)
                .Include(p => p.Yetki)
                .FirstOrDefaultAsync(p =>
                    (p.Email == emailOrSicil || p.SicilNo == emailOrSicil) &&
                    !p.IsDeleted &&
                    p.AktifMi);

            if (personel == null)
                return false;

            var now = DateTime.Now;
            if (personel.KilitlenmeTarihi.HasValue && personel.KilitlenmeTarihi.Value.AddMinutes(15) > now)
                return false;

            // Kilit süresi dolmuşsa aynı tracked entity üzerinde temizle; ayrıca SELECT/SAVE yapma.
            if (personel.KilitlenmeTarihi.HasValue)
            {
                personel.KilitlenmeTarihi = null;
                personel.BasarisizGirisSayisi = 0;
            }

            if (!await ValidatePasswordAsync(personel, password))
            {
                personel.BasarisizGirisSayisi++;
                if (personel.BasarisizGirisSayisi >= 5)
                    personel.KilitlenmeTarihi = now;

                await _context.SaveChangesAsync();
                return false;
            }

            // Başarılı girişte daha önce 3-4 ayrı SaveChanges çalışıyordu.
            // Tüm login/audit alanları tek write ile güncellenir.
            personel.BasarisizGirisSayisi = 0;
            personel.KilitlenmeTarihi = null;
            personel.SonGirisTarihi = now;
            personel.SonGirisIpAdresi = ipAddress;
            personel.SonGirisUserAgent = userAgent;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task LogoutAsync(int personelId)
        {
            await Task.CompletedTask;
        }

        public async Task<bool> IsAccountLockedAsync(Personel personel)
        {
            if (personel == null)
                return true;

            if (personel.KilitlenmeTarihi.HasValue)
            {
                if (personel.KilitlenmeTarihi.Value.AddMinutes(15) > DateTime.Now)
                    return true;
                else
                    await UnlockAccountAsync(personel.Id);
            }

            return false;
        }

        public async Task UnlockAccountAsync(int personelId)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel != null)
            {
                personel.KilitlenmeTarihi = null;
                personel.BasarisizGirisSayisi = 0;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateLastLoginAsync(int personelId)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel != null)
            {
                personel.SonGirisTarihi = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncrementFailedLoginAttemptsAsync(int personelId)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel != null)
            {
                personel.BasarisizGirisSayisi++;
                if (personel.BasarisizGirisSayisi >= 5)
                {
                    personel.KilitlenmeTarihi = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task ResetFailedLoginAttemptsAsync(int personelId)
        {
            var personel = await GetByIdAsync(personelId);
            if (personel != null)
            {
                personel.BasarisizGirisSayisi = 0;
                personel.KilitlenmeTarihi = null;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<string> SaveProfileImageAsync(int personelId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Geçerli bir dosya seçiniz.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception("Sadece resim dosyaları yüklenebilir (jpg, jpeg, png, gif, webp).");

            if (file.Length > 5 * 1024 * 1024)
                throw new Exception("Dosya boyutu 5MB'dan büyük olamaz.");

            // Create/Update akışında entity change tracker'da ise SQL'e tekrar gitmez.
            var personel = await _context.Personeller.FindAsync(personelId);
            if (personel == null || personel.IsDeleted)
                throw new Exception("Personel bulunamadı.");

            var oldProfilePath = personel.ProfilResmi;
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{personelId}_{DateTime.UtcNow.Ticks}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(
                filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/images/profiles/{fileName}";
            personel.ProfilResmi = relativePath;
            personel.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // Yeni dosya DB'ye yazıldıktan sonra eski dosyayı temizle.
            if (!string.IsNullOrWhiteSpace(oldProfilePath))
            {
                var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, oldProfilePath.TrimStart('/'));
                if (File.Exists(oldFilePath))
                    File.Delete(oldFilePath);
            }

            return relativePath;
        }

        public async Task<bool> DeleteProfileImageAsync(int personelId)
        {
            var personel = await _context.Personeller.FindAsync(personelId);
            if (personel == null || personel.IsDeleted || string.IsNullOrEmpty(personel.ProfilResmi))
                return false;

            var oldProfilePath = personel.ProfilResmi;
            personel.ProfilResmi = null;
            personel.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, oldProfilePath.TrimStart('/'));
            if (File.Exists(filePath))
                File.Delete(filePath);

            return true;
        }

        // ⭐ YENİ: Sayfalama metodu
        public async Task<PaginatedResult<Personel>> GetPersonellerAsync(PersonelFilterDto filter)
        {
            var query = _context.Personeller
                .AsNoTracking()
                // Personel Index ekranında gösterilen organizasyon alanlarını tek sorguda yükle.
                .Include(p => p.Department)
                .Include(p => p.Unit)
                .Include(p => p.Grup)
                .Include(p => p.Gorev)
                .Where(p => !p.IsDeleted);

            // Arama
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.Ad.ToLower().Contains(search) ||
                    p.Soyad.ToLower().Contains(search) ||
                    p.Email.ToLower().Contains(search) ||
                    p.SicilNo.ToLower().Contains(search)
                );
            }

            // Filtreler
            if (filter.CompanyId.HasValue)
            {
                query = query.Where(p => p.CompanyId == filter.CompanyId.Value);
            }

            if (filter.BranchId.HasValue)
            {
                query = query.Where(p => p.BranchId == filter.BranchId.Value);
            }

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(p => p.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.UnitId.HasValue)
            {
                query = query.Where(p => p.UnitId == filter.UnitId.Value);
            }

            if (filter.YetkiId.HasValue)
            {
                query = query.Where(p => p.YetkiId == filter.YetkiId.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.AktifMi == filter.IsActive.Value);
            }

            // Sıralama
            query = filter.SortBy?.ToLower() switch
            {
                "ad" => filter.SortDescending ? query.OrderByDescending(p => p.Ad) : query.OrderBy(p => p.Ad),
                "soyad" => filter.SortDescending ? query.OrderByDescending(p => p.Soyad) : query.OrderBy(p => p.Soyad),
                "sicilno" => filter.SortDescending ? query.OrderByDescending(p => p.SicilNo) : query.OrderBy(p => p.SicilNo),
                "email" => filter.SortDescending ? query.OrderByDescending(p => p.Email) : query.OrderBy(p => p.Email),
                "yetki" => filter.SortDescending ? query.OrderByDescending(p => p.Yetki.Ad) : query.OrderBy(p => p.Yetki.Ad),
                "tarih" => filter.SortDescending ? query.OrderByDescending(p => p.CreatedDate) : query.OrderBy(p => p.CreatedDate),
                _ => filter.SortDescending ? query.OrderByDescending(p => p.CreatedDate) : query.OrderBy(p => p.Ad)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Personel>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // Yardımcı metotlar
        private async Task EnsureSingleSuperAdminAssignmentAsync(int? yetkiId, int? currentPersonelId)
        {
            if (!yetkiId.HasValue)
                return;

            // Süper Admin yetkisi sistem tarafından yeniden adlandırılamaz/silinemez.
            // Bu yüzden Id request'ler arasında güvenle tutulabilir; normal personel CRUD'unda
            // her kayıtta Yetkiler tablosuna fazladan SELECT atılmaz.
            var superAdminId = await GetSuperAdminYetkiIdAsync();
            if (!superAdminId.HasValue || yetkiId.Value != superAdminId.Value)
                return;

            var alreadyAssigned = await _context.Personeller
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p =>
                    p.YetkiId == superAdminId.Value &&
                    !p.IsDeleted &&
                    (!currentPersonelId.HasValue || p.Id != currentPersonelId.Value));

            if (alreadyAssigned)
                throw new Exception("Süper Admin yetkisi yalnızca tek bir kullanıcıya atanabilir.");
        }

        private async Task<int?> GetSuperAdminYetkiIdAsync()
        {
            if (_superAdminYetkiId.HasValue)
                return _superAdminYetkiId;

            await SuperAdminYetkiLock.WaitAsync();
            try
            {
                if (_superAdminYetkiId.HasValue)
                    return _superAdminYetkiId;

                _superAdminYetkiId = await _context.Yetkiler
                    .AsNoTracking()
                    .Where(y => y.Ad == "Süper Admin" && y.IsActive && !y.IsDeleted)
                    .Select(y => (int?)y.Id)
                    .FirstOrDefaultAsync();

                return _superAdminYetkiId;
            }
            finally
            {
                SuperAdminYetkiLock.Release();
            }
        }

        private static Exception TranslatePersonelUniqueException(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                var message = sql.Message ?? string.Empty;
                if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
                    return new Exception("Bu email adresi zaten kullanılıyor!", ex);
                if (message.Contains("SicilNo", StringComparison.OrdinalIgnoreCase))
                    return new Exception("Bu Sicil No zaten kullanılıyor!", ex);

                return new Exception("Email veya Sicil No daha önce kullanılmış.", ex);
            }

            return ex;
        }

        private string GenerateSalt()
        {
            byte[] saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        private string HashPassword(string password, string salt)
        {
            if (string.IsNullOrEmpty(salt))
                throw new ArgumentException("Salt değeri boş olamaz.", nameof(salt));

            using var sha256 = SHA256.Create();
            var combined = password + salt;
            byte[] bytes = Encoding.UTF8.GetBytes(combined);
            byte[] hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
