using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class IsKazasiArastirmaService : IIsKazasiArastirmaService
    {
        private readonly ApplicationDbContext _context;

        public IsKazasiArastirmaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<IsKazasiArastirmaKategori>> GetKategorilerAsync(bool includeInactive = true)
        {
            var query = _context.IsKazasiArastirmaKategorileri.AsNoTracking().AsQueryable();
            if (!includeInactive) query = query.Where(x => x.IsActive);

            return query
                .OrderBy(x => x.Sira)
                .ThenBy(x => x.Ad)
                .ToListAsync();
        }

        public Task<IsKazasiArastirmaKategori?> GetKategoriByIdAsync(int id) =>
            _context.IsKazasiArastirmaKategorileri
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

        public async Task CreateKategoriAsync(IsKazasiArastirmaKategori kategori)
        {
            kategori.Ad = kategori.Ad.Trim();
            kategori.Aciklama = string.IsNullOrWhiteSpace(kategori.Aciklama) ? null : kategori.Aciklama.Trim();
            kategori.CreatedDate = DateTime.Now;

            var exists = await _context.IsKazasiArastirmaKategorileri
                .AnyAsync(x => x.Ad == kategori.Ad);
            if (exists) throw new InvalidOperationException("Bu isimde bir araştırma kategorisi zaten mevcut.");

            _context.IsKazasiArastirmaKategorileri.Add(kategori);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateKategoriAsync(IsKazasiArastirmaKategori kategori)
        {
            var entity = await _context.IsKazasiArastirmaKategorileri.FirstOrDefaultAsync(x => x.Id == kategori.Id)
                ?? throw new InvalidOperationException("Araştırma kategorisi bulunamadı.");

            var ad = kategori.Ad.Trim();
            if (await _context.IsKazasiArastirmaKategorileri.AnyAsync(x => x.Id != kategori.Id && x.Ad == ad))
                throw new InvalidOperationException("Bu isimde başka bir araştırma kategorisi zaten mevcut.");

            entity.Ad = ad;
            entity.Aciklama = string.IsNullOrWhiteSpace(kategori.Aciklama) ? null : kategori.Aciklama.Trim();
            entity.Sira = kategori.Sira;
            entity.IsActive = kategori.IsActive;
            entity.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteKategoriAsync(int id)
        {
            var entity = await _context.IsKazasiArastirmaKategorileri.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Araştırma kategorisi bulunamadı.");

            var hasItems = await _context.IsKazasiArastirmaMaddeleri
                .IgnoreQueryFilters()
                .AnyAsync(x => x.KategoriId == id);
            if (hasItems)
                throw new InvalidOperationException("Bu kategoriye bağlı maddeler bulunduğu için silinemez. Önce maddeleri silin veya başka kategoriye taşıyın.");

            _context.IsKazasiArastirmaKategorileri.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public Task<List<IsKazasiArastirmaMadde>> GetMaddelerAsync(int? kategoriId = null, bool includeInactive = true)
        {
            var query = _context.IsKazasiArastirmaMaddeleri
                .AsNoTracking()
                .Include(x => x.Kategori)
                .AsQueryable();

            if (kategoriId.HasValue) query = query.Where(x => x.KategoriId == kategoriId.Value);
            if (!includeInactive) query = query.Where(x => x.IsActive && x.Kategori != null && x.Kategori.IsActive);

            return query
                .OrderBy(x => x.Kategori!.Sira)
                .ThenBy(x => x.Kategori!.Ad)
                .ThenBy(x => x.Sira)
                .ThenBy(x => x.Metin)
                .ToListAsync();
        }

        public async Task<PaginatedResult<IsKazasiArastirmaMadde>> GetMaddelerPagedAsync(
            int? kategoriId = null,
            int pageNumber = 1,
            int pageSize = 10,
            bool includeInactive = true)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.IsKazasiArastirmaMaddeleri
                .AsNoTracking()
                .Include(x => x.Kategori)
                .AsQueryable();

            if (kategoriId.HasValue)
                query = query.Where(x => x.KategoriId == kategoriId.Value);

            if (!includeInactive)
                query = query.Where(x => x.IsActive && x.Kategori != null && x.Kategori.IsActive);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
            pageNumber = Math.Min(pageNumber, totalPages);

            // Eski veya silinmiş kategoriye bağlı kayıtlar listeyi çökertmez;
            // kategori bilgisi olmayan maddeler listenin sonunda gösterilir.
            var items = await query
                .OrderBy(x => x.Kategori == null ? int.MaxValue : x.Kategori.Sira)
                .ThenBy(x => x.Kategori == null ? string.Empty : x.Kategori.Ad)
                .ThenBy(x => x.Sira)
                .ThenBy(x => x.Metin)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<IsKazasiArastirmaMadde>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public Task<IsKazasiArastirmaMadde?> GetMaddeByIdAsync(int id) =>
            _context.IsKazasiArastirmaMaddeleri
                .AsNoTracking()
                .Include(x => x.Kategori)
                .FirstOrDefaultAsync(x => x.Id == id);

        public async Task CreateMaddeAsync(IsKazasiArastirmaMadde madde)
        {
            madde.Metin = madde.Metin.Trim();
            madde.Aciklama = string.IsNullOrWhiteSpace(madde.Aciklama) ? null : madde.Aciklama.Trim();
            madde.CreatedDate = DateTime.Now;

            if (!await _context.IsKazasiArastirmaKategorileri.AnyAsync(x => x.Id == madde.KategoriId))
                throw new InvalidOperationException("Seçilen kategori bulunamadı.");

            var exists = await _context.IsKazasiArastirmaMaddeleri
                .AnyAsync(x => x.KategoriId == madde.KategoriId && x.Metin == madde.Metin);
            if (exists) throw new InvalidOperationException("Bu kategoride aynı araştırma maddesi zaten mevcut.");

            _context.IsKazasiArastirmaMaddeleri.Add(madde);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMaddeAsync(IsKazasiArastirmaMadde madde)
        {
            var entity = await _context.IsKazasiArastirmaMaddeleri.FirstOrDefaultAsync(x => x.Id == madde.Id)
                ?? throw new InvalidOperationException("Araştırma maddesi bulunamadı.");

            if (!await _context.IsKazasiArastirmaKategorileri.AnyAsync(x => x.Id == madde.KategoriId))
                throw new InvalidOperationException("Seçilen kategori bulunamadı.");

            var metin = madde.Metin.Trim();
            if (await _context.IsKazasiArastirmaMaddeleri.AnyAsync(x =>
                x.Id != madde.Id && x.KategoriId == madde.KategoriId && x.Metin == metin))
                throw new InvalidOperationException("Bu kategoride aynı araştırma maddesi zaten mevcut.");

            entity.KategoriId = madde.KategoriId;
            entity.Metin = metin;
            entity.Aciklama = string.IsNullOrWhiteSpace(madde.Aciklama) ? null : madde.Aciklama.Trim();
            entity.Sira = madde.Sira;
            entity.IsActive = madde.IsActive;
            entity.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMaddeAsync(int id)
        {
            var entity = await _context.IsKazasiArastirmaMaddeleri.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Araştırma maddesi bulunamadı.");

            var used = await _context.IsKazasiArastirmaCevaplari
                .IgnoreQueryFilters()
                .AnyAsync(x => x.MaddeId == id);
            if (used)
                throw new InvalidOperationException("Bu madde daha önce bir kaza araştırmasında kullanıldığı için silinemez. Geçmiş araştırmaları korumak için maddeyi pasif yapabilirsiniz.");

            _context.IsKazasiArastirmaMaddeleri.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public Task<IsKazasi?> GetIsKazasiAsync(int isKazasiId, int branchId) =>
            _context.IsKazalari
                .AsNoTracking()
                .Where(x => x.Id == isKazasiId && x.BranchId == branchId)
                .Select(x => new IsKazasi
                {
                    Id = x.Id,
                    KazaTarihi = x.KazaTarihi,
                    PersonelId = x.PersonelId,
                    PersonelAdSoyad = x.PersonelAdSoyad,
                    BranchId = x.BranchId,
                    DepartmentId = x.DepartmentId,
                    UnitId = x.UnitId,
                    Aciklama = x.Aciklama,
                    KayipGun = x.KayipGun,
                    Department = x.Department == null ? null : new Department
                    {
                        Id = x.Department.Id,
                        DepartmentAdi = x.Department.DepartmentAdi
                    }
                })
                .FirstOrDefaultAsync();

        public Task<List<IsKazasiArastirmaKategori>> GetAktifKategorilerVeMaddelerAsync() =>
            _context.IsKazasiArastirmaKategorileri
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Sira)
                .ThenBy(x => x.Ad)
                .Select(x => new IsKazasiArastirmaKategori
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    Aciklama = x.Aciklama,
                    Sira = x.Sira,
                    IsActive = x.IsActive,
                    Maddeler = x.Maddeler
                        .Where(m => m.IsActive)
                        .OrderBy(m => m.Sira)
                        .ThenBy(m => m.Metin)
                        .Select(m => new IsKazasiArastirmaMadde
                        {
                            Id = m.Id,
                            KategoriId = m.KategoriId,
                            Metin = m.Metin,
                            Aciklama = m.Aciklama,
                            Sira = m.Sira,
                            IsActive = m.IsActive
                        })
                        .ToList()
                })
                .ToListAsync();

        public async Task<HashSet<int>> GetSeciliMaddeIdleriAsync(int isKazasiId, int branchId)
        {
            var ids = await _context.IsKazasiArastirmaCevaplari
                .AsNoTracking()
                .Where(x => x.Arastirma != null &&
                            x.Arastirma.IsKazasiId == isKazasiId &&
                            x.Arastirma.IsKazasi != null &&
                            x.Arastirma.IsKazasi.BranchId == branchId)
                .Select(x => x.MaddeId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        public Task<List<IsKazasiArastirmaMadde>> GetSeciliMaddelerAsync(int isKazasiId, int branchId) =>
            _context.IsKazasiArastirmaCevaplari
                .AsNoTracking()
                .Where(x => x.Arastirma != null &&
                            x.Arastirma.IsKazasiId == isKazasiId &&
                            x.Arastirma.IsKazasi != null &&
                            x.Arastirma.IsKazasi.BranchId == branchId &&
                            x.Madde != null)
                .Select(x => new IsKazasiArastirmaMadde
                {
                    Id = x.Madde!.Id,
                    KategoriId = x.Madde.KategoriId,
                    Metin = x.Madde.Metin,
                    Aciklama = x.Madde.Aciklama,
                    Sira = x.Madde.Sira,
                    IsActive = x.Madde.IsActive,
                    Kategori = x.Madde.Kategori == null ? null : new IsKazasiArastirmaKategori
                    {
                        Id = x.Madde.Kategori.Id,
                        Ad = x.Madde.Kategori.Ad,
                        Aciklama = x.Madde.Kategori.Aciklama,
                        Sira = x.Madde.Kategori.Sira,
                        IsActive = x.Madde.Kategori.IsActive
                    }
                })
                .OrderBy(x => x.Kategori!.Sira)
                .ThenBy(x => x.Kategori!.Ad)
                .ThenBy(x => x.Sira)
                .ThenBy(x => x.Metin)
                .ToListAsync();

        public Task<IsKazasiArastirma?> GetArastirmaBilgisiAsync(int isKazasiId, int branchId) =>
            _context.IsKazasiArastirmalari
                .AsNoTracking()
                .Include(x => x.ArastiranPersonel)
                    .ThenInclude(p => p!.Gorev)
                .FirstOrDefaultAsync(x => x.IsKazasiId == isKazasiId &&
                                          x.IsKazasi != null &&
                                          x.IsKazasi.BranchId == branchId);

        public async Task SaveArastirmaAsync(
            int isKazasiId,
            int branchId,
            int arastiranPersonelId,
            IEnumerable<int>? seciliMaddeIdleri)
        {
            var accidentExists = await _context.IsKazalari
                .AnyAsync(x => x.Id == isKazasiId && x.BranchId == branchId);
            if (!accidentExists)
                throw new InvalidOperationException("İş kazası bulunamadı veya aktif şubeye ait değil.");

            var selectedIds = (seciliMaddeIdleri ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToArray();

            if (selectedIds.Length > 0)
            {
                var validCount = await _context.IsKazasiArastirmaMaddeleri
                    .CountAsync(x => selectedIds.Contains(x.Id) && x.IsActive &&
                                     x.Kategori != null && x.Kategori.IsActive);
                if (validCount != selectedIds.Length)
                    throw new InvalidOperationException("Seçilen araştırma maddelerinden biri artık geçerli değil. Sayfayı yenileyip tekrar deneyin.");
            }

            var arastirma = await _context.IsKazasiArastirmalari
                .Include(x => x.Cevaplar)
                .FirstOrDefaultAsync(x => x.IsKazasiId == isKazasiId);

            if (arastirma == null)
            {
                arastirma = new IsKazasiArastirma
                {
                    IsKazasiId = isKazasiId,
                    ArastiranPersonelId = arastiranPersonelId,
                    ArastirmaTarihi = DateTime.Now,
                    CreatedDate = DateTime.Now
                };
                _context.IsKazasiArastirmalari.Add(arastirma);
            }
            else
            {
                arastirma.ArastiranPersonelId = arastiranPersonelId;
                arastirma.ArastirmaTarihi = DateTime.Now;
                arastirma.UpdatedDate = DateTime.Now;
            }

            var selectedSet = selectedIds.ToHashSet();
            var existingSet = arastirma.Cevaplar.Select(x => x.MaddeId).ToHashSet();

            var removed = arastirma.Cevaplar
                .Where(x => !selectedSet.Contains(x.MaddeId))
                .ToList();
            if (removed.Count > 0)
                _context.IsKazasiArastirmaCevaplari.RemoveRange(removed);

            foreach (var maddeId in selectedSet.Where(x => !existingSet.Contains(x)))
            {
                arastirma.Cevaplar.Add(new IsKazasiArastirmaCevap
                {
                    MaddeId = maddeId,
                    CreatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
