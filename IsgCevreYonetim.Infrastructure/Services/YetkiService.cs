using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class YetkiService : IYetkiService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static int _lookupCacheVersion;
        private static readonly MemoryCacheEntryOptions LookupCacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

        public YetkiService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private string CacheKey(string key) => $"yetki:{Volatile.Read(ref _lookupCacheVersion)}:{key}";
        private static void InvalidateLookupCache() => Interlocked.Increment(ref _lookupCacheVersion);

        // =============================================
        // YETKİ
        // =============================================
        public async Task<IEnumerable<Yetki>> GetAllYetkilerAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("yetkiler:all"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Yetkiler.AsNoTracking().Where(y => !y.IsDeleted).OrderBy(y => y.Ad).ToListAsync();
            }) ?? new List<Yetki>();
        }

        public async Task<Yetki?> GetYetkiByIdAsync(int id)
        {
            return await _context.Yetkiler
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == id && !y.IsDeleted);
        }

        public async Task<Yetki> CreateYetkiAsync(Yetki yetki)
        {
            try
            {
                if (string.IsNullOrEmpty(yetki.Ad))
                {
                    throw new Exception("Yetki adı zorunludur!");
                }

                if (string.Equals(yetki.Ad.Trim(), "Süper Admin", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Süper Admin yetkisi sistem tarafından yönetilir ve ikinci kez oluşturulamaz.");

                if (await _context.Yetkiler.AnyAsync(y => y.Ad == yetki.Ad && !y.IsDeleted))
                {
                    throw new Exception($"'{yetki.Ad}' adı zaten kullanılıyor!");
                }

                yetki.CreatedDate = DateTime.Now;
                yetki.IsActive = true;
                await _context.Yetkiler.AddAsync(yetki);
                await _context.SaveChangesAsync();
                InvalidateLookupCache();
                return yetki;
            }
            catch (DbUpdateException)
            {
                throw new Exception("Yetki kaydedilirken veritabanı işlemi tamamlanamadı. Aynı adda başka bir kayıt bulunabilir.");
            }
        }

        public async Task UpdateYetkiAsync(Yetki yetki)
        {
            try
            {
                var existing = await _context.Yetkiler.FirstOrDefaultAsync(x => x.Id == yetki.Id && !x.IsDeleted)
                    ?? throw new Exception("Yetki bulunamadı!");

                if (string.Equals(existing.Ad, "Süper Admin", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Süper Admin yetkisi yeniden adlandırılamaz veya pasif yapılamaz.");

                if (string.Equals(yetki.Ad?.Trim(), "Süper Admin", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Süper Admin adı yalnız sistemin oluşturduğu tek yetki profili için kullanılabilir.");

                existing.Ad = yetki.Ad;
                existing.IsActive = yetki.IsActive;
                existing.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                InvalidateLookupCache();
            }
            catch (DbUpdateException)
            {
                throw new Exception("Yetki güncellenirken veritabanı işlemi tamamlanamadı. Aynı adda başka bir kayıt bulunabilir.");
            }
        }

        public async Task DeleteYetkiAsync(int id)
        {
            var yetki = await _context.Yetkiler.IgnoreQueryFilters()
                .FirstOrDefaultAsync(y => y.Id == id && !y.IsDeleted)
                ?? throw new Exception("Yetki bulunamadı!");

            if (string.Equals(yetki.Ad, "Süper Admin", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Süper Admin yetkisi silinemez.");

            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.YetkiId == id))
                throw new Exception("Bu yetki profiline bağlı personeller bulunuyor. Önce personellerin yetkisini değiştirin.");

            var subeRows = await _context.YetkiSubeler.IgnoreQueryFilters().Where(x => x.YetkiId == id).ToListAsync();
            var pageRows = await _context.YetkiSayfalar.IgnoreQueryFilters().Where(x => x.YetkiId == id).ToListAsync();
            _context.YetkiSayfalar.RemoveRange(pageRows);
            _context.YetkiSubeler.RemoveRange(subeRows);
            _context.Yetkiler.Remove(yetki);
            await _context.SaveChangesAsync();
            InvalidateLookupCache();
        }

        public async Task<bool> YetkiExistsAsync(string ad)
        {
            return await _context.Yetkiler.AnyAsync(y => y.Ad == ad && !y.IsDeleted);
        }

        // ⭐ YETKİ SAYFALAMA - YENİ METOT
        public async Task<PaginatedResult<Yetki>> GetYetkilerAsync(ReferenceFilterDto filter)
        {
            var query = _context.Yetkiler.AsNoTracking().Where(y => !y.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(y =>
                    y.Ad.ToLower().Contains(search)
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(y => y.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "ad" => filter.SortDescending ? query.OrderByDescending(y => y.Ad) : query.OrderBy(y => y.Ad),
                "id" => filter.SortDescending ? query.OrderByDescending(y => y.Id) : query.OrderBy(y => y.Id),
                "tarih" => filter.SortDescending ? query.OrderByDescending(y => y.CreatedDate) : query.OrderBy(y => y.CreatedDate),
                _ => filter.SortDescending ? query.OrderByDescending(y => y.CreatedDate) : query.OrderBy(y => y.Ad)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Yetki>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // =============================================
        // SAYFA
        // =============================================
        public async Task<IEnumerable<Sayfa>> GetAllSayfalarAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("sayfalar:all"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Sayfalar.AsNoTracking().Where(s => !s.IsDeleted)
                    .OrderBy(s => s.ParentId).ThenBy(s => s.Sira).ToListAsync();
            }) ?? new List<Sayfa>();
        }

        public async Task<Sayfa?> GetSayfaByIdAsync(int id)
        {
            return await _context.Sayfalar
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        }

        public async Task<Sayfa> CreateSayfaAsync(Sayfa sayfa)
        {
            try
            {
                if (string.IsNullOrEmpty(sayfa.Url))
                {
                    throw new Exception("Sayfa URL'si zorunludur!");
                }

                if (await _context.Sayfalar.AnyAsync(s => s.Url == sayfa.Url && !s.IsDeleted))
                {
                    throw new Exception($"'{sayfa.Url}' URL'si zaten kullanılıyor!");
                }

                if (string.IsNullOrEmpty(sayfa.Ad))
                {
                    throw new Exception("Sayfa adı zorunludur!");
                }

                sayfa.CreatedDate = DateTime.Now;
                sayfa.IsActive = true;
                await _context.Sayfalar.AddAsync(sayfa);
                await _context.SaveChangesAsync();
                InvalidateLookupCache();
                return sayfa;
            }
            catch (DbUpdateException)
            {
                throw new Exception("Sayfa kaydedilirken veritabanı işlemi tamamlanamadı. Aynı URL veya ad ile başka bir kayıt bulunabilir.");
            }
        }

        public async Task UpdateSayfaAsync(Sayfa sayfa)
        {
            try
            {
                var existing = await _context.Sayfalar.FirstOrDefaultAsync(x => x.Id == sayfa.Id && !x.IsDeleted)
                    ?? throw new Exception("Sayfa bulunamadı!");
                existing.Ad = sayfa.Ad;
                existing.Url = sayfa.Url;
                existing.Icon = sayfa.Icon;
                existing.ParentId = sayfa.ParentId;
                existing.Sira = sayfa.Sira;
                existing.IsActive = sayfa.IsActive;
                existing.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                InvalidateLookupCache();
            }
            catch (DbUpdateException)
            {
                throw new Exception("Sayfa güncellenirken veritabanı işlemi tamamlanamadı. Aynı URL veya ad ile başka bir kayıt bulunabilir.");
            }
        }

        public async Task DeleteSayfaAsync(int id)
        {
            var sayfa = await _context.Sayfalar.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted)
                ?? throw new Exception("Sayfa bulunamadı!");

            if (await _context.Sayfalar.IgnoreQueryFilters().AnyAsync(s => s.ParentId == id && !s.IsDeleted))
                throw new Exception("Bu sayfaya bağlı alt sayfalar bulunuyor. Önce alt sayfaları silin.");

            var permissionRows = await _context.YetkiSayfalar.IgnoreQueryFilters()
                .Where(x => x.SayfaId == id).ToListAsync();
            _context.YetkiSayfalar.RemoveRange(permissionRows);
            _context.Sayfalar.Remove(sayfa);
            await _context.SaveChangesAsync();
            InvalidateLookupCache();
        }

        // ⭐ SAYFA SAYFALAMA - YENİ METOT
        public async Task<PaginatedResult<Sayfa>> GetSayfalarAsync(ReferenceFilterDto filter)
        {
            var query = _context.Sayfalar.AsNoTracking().Where(s => !s.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(s =>
                    s.Ad.ToLower().Contains(search) ||
                    s.Url.ToLower().Contains(search)
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "ad" => filter.SortDescending ? query.OrderByDescending(s => s.Ad) : query.OrderBy(s => s.Ad),
                "url" => filter.SortDescending ? query.OrderByDescending(s => s.Url) : query.OrderBy(s => s.Url),
                "sira" => filter.SortDescending ? query.OrderByDescending(s => s.Sira) : query.OrderBy(s => s.Sira),
                "tarih" => filter.SortDescending ? query.OrderByDescending(s => s.CreatedDate) : query.OrderBy(s => s.CreatedDate),
                _ => filter.SortDescending ? query.OrderByDescending(s => s.CreatedDate) : query.OrderBy(s => s.Ad)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Sayfa>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // =============================================
        // YETKİLENDİRME: Yetki + Şube + Sayfa
        // =============================================
        public async Task<IEnumerable<YetkiSayfa>> GetYetkiSayfalarByYetkiAndBranchIdAsync(int yetkiId, int branchId)
        {
            return await _context.YetkiSayfalar
                .AsNoTracking()
                .Include(ys => ys.Sayfa)
                .Where(ys => ys.YetkiId == yetkiId && ys.BranchId == branchId && !ys.IsDeleted)
                .OrderBy(ys => ys.Sayfa.Sira)
                .ToListAsync();
        }

        public async Task<bool> YetkilendirAsync(int yetkiId, int branchId, int sayfaId, bool ekle, bool guncelle, bool sil, bool goster)
        {
            if (yetkiId <= 0 || branchId <= 0 || sayfaId <= 0)
                throw new Exception("Yetki, şube veya sayfa bilgisi geçersiz!");

            var yetkiVar = await _context.Yetkiler
                .AnyAsync(x => x.Id == yetkiId && !x.IsDeleted);

            var branchVar = await _context.Branches
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Id == branchId && !x.IsDeleted);

            var sayfaVar = await _context.Sayfalar
                .AnyAsync(x => x.Id == sayfaId && !x.IsDeleted);

            if (!yetkiVar || !branchVar || !sayfaVar)
                throw new Exception("Yetki, şube veya sayfa bulunamadı!");

            var row = await _context.YetkiSayfalar.FirstOrDefaultAsync(x =>
                x.YetkiId == yetkiId &&
                x.BranchId == branchId &&
                x.SayfaId == sayfaId);

            if (row == null)
            {
                row = new YetkiSayfa
                {
                    YetkiId = yetkiId,
                    BranchId = branchId,
                    SayfaId = sayfaId,
                    Ekle = ekle,
                    Guncelle = guncelle,
                    Sil = sil,
                    Goster = goster,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false
                };

                await _context.YetkiSayfalar.AddAsync(row);
            }
            else
            {
                row.Ekle = ekle;
                row.Guncelle = guncelle;
                row.Sil = sil;
                row.Goster = goster;
                row.UpdatedDate = DateTime.Now;
                row.IsActive = true;
                row.IsDeleted = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> YetkilendirTopluAsync(
            int yetkiId,
            int branchId,
            IEnumerable<YetkiSayfa> permissions,
            bool lokasyonSecebilir)
        {
            if (yetkiId <= 0 || branchId <= 0)
                throw new Exception("Yetki veya şube bilgisi geçersiz!");

            var requested = permissions
                .GroupBy(x => x.SayfaId)
                .Select(g => g.Last())
                .ToList();

            if (requested.Count == 0)
                throw new Exception("Yetki bilgileri gönderilmedi!");

            var pageIds = requested.Select(x => x.SayfaId).ToArray();
            var validPageIds = await _context.Sayfalar
                .AsNoTracking()
                .Where(x => pageIds.Contains(x.Id) && !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync();

            if (validPageIds.Count != pageIds.Distinct().Count())
                throw new Exception("Gönderilen sayfalardan biri bulunamadı!");

            var targetExists = await _context.Yetkiler.AnyAsync(x => x.Id == yetkiId && !x.IsDeleted)
                && await _context.Branches.IgnoreQueryFilters().AnyAsync(x => x.Id == branchId && !x.IsDeleted);
            if (!targetExists)
                throw new Exception("Yetki veya şube bulunamadı!");

            var existingRows = await _context.YetkiSayfalar
                .Where(x => x.YetkiId == yetkiId && x.BranchId == branchId && pageIds.Contains(x.SayfaId))
                .ToListAsync();
            var rowMap = existingRows.ToDictionary(x => x.SayfaId);
            var now = DateTime.Now;

            foreach (var permission in requested)
            {
                if (!rowMap.TryGetValue(permission.SayfaId, out var row))
                {
                    row = new YetkiSayfa
                    {
                        YetkiId = yetkiId,
                        BranchId = branchId,
                        SayfaId = permission.SayfaId,
                        CreatedDate = now
                    };
                    _context.YetkiSayfalar.Add(row);
                }
                else
                {
                    row.UpdatedDate = now;
                }

                row.Ekle = permission.Ekle;
                row.Guncelle = permission.Guncelle;
                row.Sil = permission.Sil;
                row.Goster = permission.Goster;
                row.IsActive = true;
                row.IsDeleted = false;
            }

            var subeRow = await _context.YetkiSubeler
                .FirstOrDefaultAsync(x => x.YetkiId == yetkiId && x.BranchId == branchId);
            if (subeRow == null)
            {
                subeRow = new YetkiSube
                {
                    YetkiId = yetkiId,
                    BranchId = branchId,
                    CreatedDate = now
                };
                _context.YetkiSubeler.Add(subeRow);
            }
            else
            {
                subeRow.UpdatedDate = now;
            }

            subeRow.LokasyonSecebilir = lokasyonSecebilir;
            subeRow.IsActive = true;
            subeRow.IsDeleted = false;

            // Tüm sayfa izinleri + lokasyon ayarı tek transaction / SaveChanges ile yazılır.
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> YetkiSubeAyarlaAsync(int yetkiId, int branchId, bool lokasyonSecebilir)
        {
            if (yetkiId <= 0 || branchId <= 0)
                throw new Exception("Yetki veya şube bilgisi geçersiz!");

            var row = await _context.YetkiSubeler.FirstOrDefaultAsync(x =>
                x.YetkiId == yetkiId && x.BranchId == branchId);

            if (row == null)
            {
                row = new YetkiSube
                {
                    YetkiId = yetkiId,
                    BranchId = branchId,
                    LokasyonSecebilir = lokasyonSecebilir,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false
                };

                await _context.YetkiSubeler.AddAsync(row);
            }
            else
            {
                row.LokasyonSecebilir = lokasyonSecebilir;
                row.UpdatedDate = DateTime.Now;
                row.IsActive = true;
                row.IsDeleted = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LokasyonSecebilirAsync(int yetkiId, int branchId)
        {
            return await _context.YetkiSubeler.AnyAsync(x =>
                x.YetkiId == yetkiId &&
                x.BranchId == branchId &&
                x.LokasyonSecebilir &&
                x.IsActive &&
                !x.IsDeleted);
        }

        public async Task<bool> KullaniciLokasyonSecilebilirMiAsync(int personelId)
        {
            var personel = await _context.Personeller
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == personelId &&
                    !p.IsDeleted &&
                    p.AktifMi);

            if (personel?.YetkiId == null || personel.BranchId == null)
                return false;

            return await _context.YetkiSubeler.AnyAsync(x =>
                x.YetkiId == personel.YetkiId.Value &&
                x.BranchId == personel.BranchId.Value &&
                x.LokasyonSecebilir &&
                x.IsActive &&
                !x.IsDeleted);
        }

        public async Task<bool> KullaniciYetkiliMiAsync(int personelId, string sayfaUrl, string islem, int? branchId = null)
        {
            var personel = await _context.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personelId && !p.IsDeleted && p.AktifMi);
            if (personel?.YetkiId == null) return false;
            var effectiveBranch = branchId ?? personel.BranchId;
            if (!effectiveBranch.HasValue) return false;
            var permission = await _context.YetkiSayfalar
                .AsNoTracking()
                .Include(x => x.Sayfa)
                .FirstOrDefaultAsync(x =>
                    x.YetkiId == personel.YetkiId &&
                    x.BranchId == effectiveBranch.Value &&
                    x.Sayfa.Url == sayfaUrl &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.Sayfa.IsActive &&
                    !x.Sayfa.IsDeleted);
            // Ekranın alt actionları (Detay, Sil, Export vb.) menüdeki controller/index sayfasının iznini kullanır.
            if (permission == null)
            {
                var segments = sayfaUrl.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var baseUrl = $"/{segments[0]}/Index";
                    permission = await _context.YetkiSayfalar
                        .AsNoTracking()
                        .Include(x => x.Sayfa)
                        .FirstOrDefaultAsync(x =>
                            x.YetkiId == personel.YetkiId &&
                            x.BranchId == effectiveBranch.Value &&
                            x.Sayfa.Url == baseUrl &&
                            x.IsActive &&
                            !x.IsDeleted &&
                            x.Sayfa.IsActive &&
                            !x.Sayfa.IsDeleted);
                }
            }
            if (permission == null) return false;
            return islem switch { "Ekle" => permission.Ekle, "Guncelle" => permission.Guncelle, "Sil" => permission.Sil, "Goster" => permission.Goster, _ => false };
        }

        public async Task<IEnumerable<Sayfa>> GetYetkiliSayfalarAsync(int personelId, int? branchId = null)
        {
            var personel = await _context.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personelId && !p.IsDeleted && p.AktifMi);
            var effectiveBranch = branchId ?? personel?.BranchId;
            if (personel?.YetkiId == null || !effectiveBranch.HasValue) return Array.Empty<Sayfa>();
            return await _context.YetkiSayfalar.AsNoTracking().Include(x => x.Sayfa)
                .Where(x => x.YetkiId == personel.YetkiId && x.BranchId == effectiveBranch.Value && x.Goster && x.IsActive && !x.IsDeleted && x.Sayfa.IsActive && !x.Sayfa.IsDeleted)
                .OrderBy(x => x.Sayfa.Sira).Select(x => x.Sayfa).ToListAsync();
        }

    }
}