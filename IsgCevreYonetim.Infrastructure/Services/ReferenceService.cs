using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using IsgCevreYonetim.Shared.DTOs;
using System.Linq.Expressions;
using Microsoft.Extensions.Caching.Memory;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class ReferenceService : IReferenceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static int _cacheVersion;
        private static readonly MemoryCacheEntryOptions LookupCacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        public ReferenceService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private string CacheKey(string key) => $"ref:{Volatile.Read(ref _cacheVersion)}:{key}";
        private static void InvalidateCache() => Interlocked.Increment(ref _cacheVersion);

        // ---------- İL ----------
        public async Task<IEnumerable<Il>> GetAllIllerAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("iller:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Iller.AsNoTracking().OrderBy(i => i.Ad).ToListAsync(); }) ?? new List<Il>();
        }

        public async Task<Il?> GetIlByIdAsync(int id)
        {
            return await _context.Iller
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        }

        public async Task<Il> CreateIlAsync(Il il)
        {
            il.CreatedDate = DateTime.Now;
            il.IsActive = true;
            await _context.Iller.AddAsync(il);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return il;
        }

        public async Task UpdateIlAsync(Il il)
        {
            var existing = await _context.Iller.FirstOrDefaultAsync(x => x.Id == il.Id && !x.IsDeleted)
                ?? throw new Exception("İl bulunamadı.");
            existing.Kod = il.Kod;
            existing.Ad = il.Ad;
            existing.IsActive = il.IsActive;
            existing.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteIlAsync(int id)
        {
            var il = await _context.Iller.FirstOrDefaultAsync(x => x.Id == id);
            if (il == null) return;
            if (await _context.Ilceler.IgnoreQueryFilters().AnyAsync(i => i.IlId == id))
                throw new Exception("Bu ile bağlı ilçeler bulunuyor. Önce ilçeleri silmelisiniz.");
            if (await _context.Branches.IgnoreQueryFilters().AnyAsync(b => b.IlId == id))
                throw new Exception("Bu ile bağlı şubeler bulunuyor. Önce şubeleri silmelisiniz.");
            _context.Iller.Remove(il);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> IlExistsAsync(string kod, string ad)
        {
            return await _context.Iller
                .AnyAsync(i => (i.Kod == kod || i.Ad == ad) && !i.IsDeleted);
        }

        // ---------- İLÇE ----------
        public async Task<IEnumerable<Ilce>> GetAllIlcelerAsync()
        {
            return await _context.Ilceler
                    .AsNoTracking()
                    .Include(i => i.Il)
                    .OrderBy(i => i.Ad)
                    .ToListAsync();
        }

        public async Task<IEnumerable<Ilce>> GetIlcelerByIlIdAsync(int ilId)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"ilceler:il:{ilId}"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Ilceler.AsNoTracking().Where(i => i.IlId == ilId && !i.IsDeleted).OrderBy(i => i.Ad).ToListAsync(); }) ?? new List<Ilce>();
        }

        public async Task<Ilce?> GetIlceByIdAsync(int id)
        {
            return await _context.Ilceler
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        }

        public async Task<Ilce> CreateIlceAsync(Ilce ilce)
        {
            ilce.CreatedDate = DateTime.Now;
            ilce.IsActive = true;
            await _context.Ilceler.AddAsync(ilce);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return ilce;
        }

        public async Task UpdateIlceAsync(Ilce ilce)
        {
            var existing = await _context.Ilceler.FirstOrDefaultAsync(x => x.Id == ilce.Id && !x.IsDeleted)
                ?? throw new Exception("İlçe bulunamadı.");
            existing.Ad = ilce.Ad;
            existing.IlId = ilce.IlId;
            existing.IsActive = ilce.IsActive;
            existing.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteIlceAsync(int id)
        {
            var ilce = await _context.Ilceler.FirstOrDefaultAsync(x => x.Id == id);
            if (ilce == null) return;
            if (await _context.Branches.IgnoreQueryFilters().AnyAsync(b => b.IlceId == id))
                throw new Exception("Bu ilçeye bağlı şubeler bulunuyor. Önce şubeleri silmelisiniz.");
            _context.Ilceler.Remove(ilce);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> IlceExistsAsync(int ilId, string ad)
        {
            return await _context.Ilceler
                .IgnoreQueryFilters()
                .AnyAsync(i => i.IlId == ilId && i.Ad == ad && !i.IsDeleted);
        }

        // ---------- CİNSİYET ----------
        public async Task<IEnumerable<Cinsiyet>> GetAllCinsiyetlerAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("cinsiyetler:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Cinsiyetler.AsNoTracking().OrderBy(c => c.Ad).ToListAsync(); }) ?? new List<Cinsiyet>();
        }

        public async Task<Cinsiyet?> GetCinsiyetByIdAsync(int id)
        {
            return await _context.Cinsiyetler
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<Cinsiyet> CreateCinsiyetAsync(Cinsiyet cinsiyet)
        {
            cinsiyet.CreatedDate = DateTime.Now;
            cinsiyet.IsActive = true;
            await _context.Cinsiyetler.AddAsync(cinsiyet);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return cinsiyet;
        }

        public async Task UpdateCinsiyetAsync(Cinsiyet cinsiyet)
        {
            var existing = await _context.Cinsiyetler.FirstOrDefaultAsync(x => x.Id == cinsiyet.Id && !x.IsDeleted)
                ?? throw new Exception("Cinsiyet bulunamadı.");
            existing.Ad = cinsiyet.Ad;
            existing.IsActive = cinsiyet.IsActive;
            existing.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteCinsiyetAsync(int id)
        {
            var cinsiyet = await _context.Cinsiyetler.FirstOrDefaultAsync(x => x.Id == id);
            if (cinsiyet == null) return;
            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.CinsiyetId == id))
                throw new Exception("Bu cinsiyete bağlı personeller bulunuyor. Önce personelleri silmelisiniz.");
            _context.Cinsiyetler.Remove(cinsiyet);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> CinsiyetExistsAsync(string ad)
        {
            return await _context.Cinsiyetler
                .AnyAsync(c => c.Ad == ad && !c.IsDeleted);
        }

        // ---------- GÖREV ----------
        public async Task<IEnumerable<Gorev>> GetAllGorevlerAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("gorevler:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Gorevler.AsNoTracking().OrderBy(g => g.Ad).ToListAsync(); }) ?? new List<Gorev>();
        }

        public async Task<Gorev?> GetGorevByIdAsync(int id)
        {
            return await _context.Gorevler
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
        }

        public async Task<Gorev> CreateGorevAsync(Gorev gorev)
        {
            gorev.CreatedDate = DateTime.Now;
            gorev.IsActive = true;
            await _context.Gorevler.AddAsync(gorev);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return gorev;
        }

        public async Task UpdateGorevAsync(Gorev gorev)
        {
            var existing = await _context.Gorevler.FirstOrDefaultAsync(x => x.Id == gorev.Id && !x.IsDeleted)
                ?? throw new Exception("Görev bulunamadı.");
            existing.Ad = gorev.Ad;
            existing.IsActive = gorev.IsActive;
            existing.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteGorevAsync(int id)
        {
            var gorev = await _context.Gorevler.FirstOrDefaultAsync(x => x.Id == id);
            if (gorev == null) return;
            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.GorevId == id))
                throw new Exception("Bu göreve bağlı personeller bulunuyor. Önce personelleri silmelisiniz.");
            if (await _context.IsKazalari.IgnoreQueryFilters().AnyAsync(x => x.GorevId == id))
                throw new Exception("Bu göreve bağlı iş kazası kayıtları bulunuyor. Önce iş kazası kayıtlarını silmelisiniz.");
            _context.Gorevler.Remove(gorev);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> GorevExistsAsync(string ad)
        {
            return await _context.Gorevler
                .AnyAsync(g => g.Ad == ad && !g.IsDeleted);
        }

        // ---------- GRUP ----------
        public async Task<IEnumerable<Grup>> GetAllGruplarAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("gruplar:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Gruplar.AsNoTracking().OrderBy(g => g.Ad).ToListAsync(); }) ?? new List<Grup>();
        }

        public async Task<Grup?> GetGrupByIdAsync(int id)
        {
            return await _context.Gruplar
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
        }

        public async Task<Grup> CreateGrupAsync(Grup grup)
        {
            grup.CreatedDate = DateTime.Now;
            grup.IsActive = true;
            await _context.Gruplar.AddAsync(grup);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return grup;
        }

        public async Task UpdateGrupAsync(Grup grup)
        {
            var existing = await _context.Gruplar.FirstOrDefaultAsync(x => x.Id == grup.Id && !x.IsDeleted)
                ?? throw new Exception("Grup bulunamadı.");
            existing.Ad = grup.Ad;
            existing.IsActive = grup.IsActive;
            existing.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteGrupAsync(int id)
        {
            var grup = await _context.Gruplar.FirstOrDefaultAsync(x => x.Id == id);
            if (grup == null) return;
            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.GrupId == id))
                throw new Exception("Bu gruba bağlı personeller bulunuyor. Önce personelleri silmelisiniz.");
            if (await _context.IsKazalari.IgnoreQueryFilters().AnyAsync(x => x.GrupId == id))
                throw new Exception("Bu gruba bağlı iş kazası kayıtları bulunuyor. Önce iş kazası kayıtlarını silmelisiniz.");
            _context.Gruplar.Remove(grup);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> GrupExistsAsync(string ad)
        {
            return await _context.Gruplar
                .AnyAsync(g => g.Ad == ad && !g.IsDeleted);
        }

        // ---------- SAYFALAMA ----------
        public async Task<PaginatedResult<Il>> GetIllerAsync(ReferenceFilterDto filter)
        {
            var query = _context.Iller.AsNoTracking().Where(i => !i.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(i =>
                    i.Ad.ToLower().Contains(search) ||
                    i.Kod.ToLower().Contains(search)
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(i => i.IsActive == filter.IsActive.Value);
            }

            // Sıralama işlemini switch expression ile yap
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = filter.SortBy.ToLower() switch
                {
                    "ad" => filter.SortDescending ? query.OrderByDescending(i => i.Ad) : query.OrderBy(i => i.Ad),
                    "kod" => filter.SortDescending ? query.OrderByDescending(i => i.Kod) : query.OrderBy(i => i.Kod),
                    _ => filter.SortDescending ? query.OrderByDescending(i => i.CreatedDate) : query.OrderBy(i => i.Ad)
                };
            }
            else
            {
                query = query.OrderBy(i => i.Ad);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Il>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Ilce>> GetIlcelerAsync(ReferenceFilterDto filter)
        {
            var query = _context.Ilceler
                .AsNoTracking()
                .Include(i => i.Il)
                .Where(i => !i.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(i =>
                    i.Ad.ToLower().Contains(search) ||
                    (i.Il != null && i.Il.Ad.ToLower().Contains(search))
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(i => i.IsActive == filter.IsActive.Value);
            }

            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = filter.SortBy.ToLower() switch
                {
                    "ad" => filter.SortDescending ? query.OrderByDescending(i => i.Ad) : query.OrderBy(i => i.Ad),
                    "il" => filter.SortDescending ? query.OrderByDescending(i => i.Il.Ad) : query.OrderBy(i => i.Il.Ad),
                    _ => filter.SortDescending ? query.OrderByDescending(i => i.CreatedDate) : query.OrderBy(i => i.Ad)
                };
            }
            else
            {
                query = query.OrderBy(i => i.Ad);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Ilce>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Gorev>> GetGorevlerAsync(ReferenceFilterDto filter)
        {
            var query = _context.Gorevler.AsNoTracking().Where(g => !g.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(g => g.Ad.ToLower().Contains(search));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(g => g.IsActive == filter.IsActive.Value);
            }

            if (!string.IsNullOrEmpty(filter.SortBy) && filter.SortBy.ToLower() == "ad")
            {
                query = filter.SortDescending ? query.OrderByDescending(g => g.Ad) : query.OrderBy(g => g.Ad);
            }
            else
            {
                query = query.OrderBy(g => g.Ad);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Gorev>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Grup>> GetGruplarAsync(ReferenceFilterDto filter)
        {
            var query = _context.Gruplar.AsNoTracking().Where(g => !g.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(g => g.Ad.ToLower().Contains(search));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(g => g.IsActive == filter.IsActive.Value);
            }

            if (!string.IsNullOrEmpty(filter.SortBy) && filter.SortBy.ToLower() == "ad")
            {
                query = filter.SortDescending ? query.OrderByDescending(g => g.Ad) : query.OrderBy(g => g.Ad);
            }
            else
            {
                query = query.OrderBy(g => g.Ad);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Grup>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Cinsiyet>> GetCinsiyetlerAsync(ReferenceFilterDto filter)
        {
            var query = _context.Cinsiyetler.AsNoTracking().Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(c => c.Ad.ToLower().Contains(search));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }

            if (!string.IsNullOrEmpty(filter.SortBy) && filter.SortBy.ToLower() == "ad")
            {
                query = filter.SortDescending ? query.OrderByDescending(c => c.Ad) : query.OrderBy(c => c.Ad);
            }
            else
            {
                query = query.OrderBy(c => c.Ad);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Cinsiyet>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }
    }
}
