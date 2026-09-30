using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class IsKazasiService : IIsKazasiService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public IsKazasiService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IsKazasi?> GetByIdAsync(int id, int? branchId = null)
        {
            IQueryable<IsKazasi> query = _context.IsKazalari
                .AsNoTracking()
                .AsSplitQuery()
                .Include(i => i.Personel).ThenInclude(p => p!.Branch)
                .Include(i => i.Personel).ThenInclude(p => p!.Department)
                .Include(i => i.Personel).ThenInclude(p => p!.Gorev)
                .Include(i => i.Personel).ThenInclude(p => p!.Cinsiyet)
                .Include(i => i.SorumluAmirPersonel)
                .Include(i => i.Sahitler).ThenInclude(s => s.Personel)
                .Include(i => i.DuzelticiFaaliyetler).ThenInclude(f => f.Dosyalar)
                .Include(i => i.Branch)
                .Include(i => i.Department)
                .Include(i => i.Unit)
                .Include(i => i.Gorev)
                .Include(i => i.Grup)
                .Include(i => i.Vardiya)
                .Include(i => i.MudahaleSekli)
                .Include(i => i.Dosyalar)
                .Where(i => i.Id == id);

            if (branchId.HasValue)
                query = query.Where(i => i.BranchId == branchId.Value);

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<IsKazasi>> GetAllAsync()
        {
            return await _context.IsKazalari
                .AsNoTracking()
                .Include(i => i.Personel)
                .Include(i => i.Branch)
                .Include(i => i.Department)
                .Include(i => i.Unit)
                .Include(i => i.Gorev)
                .Include(i => i.Grup)
                .Include(i => i.Vardiya)
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.KazaTarihi)
                .ToListAsync();
        }

        public async Task<IsKazasi> CreateAsync(IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds, int branchId)
        {
            // EnableRetryOnFailure etkin olduğunda kullanıcı tarafından başlatılan
            // transaction'ın tamamı execution strategy içinde çalışmalıdır.
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    await MudahaleSekliniDogrulaVeEsitleAsync(isKazasi);
                    isKazasi.KayipGun = isKazasi.HesaplaKayipGun();
                    isKazasi.CreatedDate = DateTime.Now;
                    isKazasi.IsDeleted = false;
                    isKazasi.IsActive = true;

                    await _context.IsKazalari.AddAsync(isKazasi);
                    await _context.SaveChangesAsync();

                    await SahitleriGuncelleAsync(isKazasi.Id, sahitPersonelIds, branchId);

                    if (dosyalar != null && dosyalar.Any())
                    {
                        await SaveDosyalarAsync(isKazasi.Id, dosyalar);
                    }

                    await transaction.CommitAsync();
                    return isKazasi;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task UpdateAsync(IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds, int branchId)
        {
            await MudahaleSekliniDogrulaVeEsitleAsync(isKazasi);

            var existing = await _context.IsKazalari
                .FirstOrDefaultAsync(i =>
                    i.Id == isKazasi.Id &&
                    i.BranchId == branchId);

            if (existing == null)
            {
                throw new Exception("İş kazası bulunamadı!");
            }

            // Posted entity'yi attach etmek yerine mevcut tracked kayıt üzerinde
            // alanları güncelle. Nullable alanlar doğrudan atanır; kullanıcı formda
            // bir değeri temizlediğinde eski veri yanlışlıkla korunmaz.
            existing.KazaTarihi = isKazasi.KazaTarihi;
            existing.Aciklama = isKazasi.Aciklama;
            existing.Mudahale = isKazasi.Mudahale;
            existing.MudahaleSekliId = isKazasi.MudahaleSekliId;
            existing.RaporBaslangic = isKazasi.RaporBaslangic;
            existing.RaporBitis = isKazasi.RaporBitis;
            existing.PersonelId = isKazasi.PersonelId;
            existing.PersonelAdSoyad = isKazasi.PersonelAdSoyad;
            existing.SorumluAmirPersonelId = isKazasi.SorumluAmirPersonelId;
            existing.SorumluAmirAdSoyad = isKazasi.SorumluAmirAdSoyad;
            existing.BranchId = isKazasi.BranchId;
            existing.DepartmentId = isKazasi.DepartmentId;
            existing.UnitId = isKazasi.UnitId;
            existing.GorevId = isKazasi.GorevId;
            existing.GrupId = isKazasi.GrupId;
            existing.IseGirisTarihi = isKazasi.IseGirisTarihi;
            existing.VardiyaId = isKazasi.VardiyaId;
            existing.KayipGun = isKazasi.HesaplaKayipGun();
            existing.UpdatedDate = DateTime.Now;

            if (dosyalar != null && dosyalar.Any())
            {
                await AddDosyalarToContextAsync(existing.Id, dosyalar);
            }

            await _context.SaveChangesAsync();
            await SahitleriGuncelleAsync(existing.Id, sahitPersonelIds, branchId);
        }

        private async Task MudahaleSekliniDogrulaVeEsitleAsync(IsKazasi isKazasi)
        {
            if (!isKazasi.MudahaleSekliId.HasValue || isKazasi.MudahaleSekliId.Value <= 0)
                throw new Exception("Lütfen müdahale şekli seçiniz.");

            var mudahaleSekli = await _context.MudahaleSekilleri
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == isKazasi.MudahaleSekliId.Value && x.IsActive && !x.IsDeleted);

            if (mudahaleSekli == null)
                throw new Exception("Seçilen müdahale şekli geçerli veya aktif değil.");

            // Raporlar ve eski entegrasyonlar için metin alanı snapshot olarak korunur.
            isKazasi.Mudahale = mudahaleSekli.Ad;
        }

        private async Task SahitleriGuncelleAsync(int isKazasiId, List<int>? sahitPersonelIds, int branchId)
        {
            var ids = (sahitPersonelIds ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
            var mevcut = await _context.IsKazasiSahitleri
                .Where(x => x.IsKazasiId == isKazasiId)
                .ToListAsync();

            if (mevcut.Count > 0)
                _context.IsKazasiSahitleri.RemoveRange(mevcut);

            if (ids.Count > 0)
            {
                var personeller = await _context.Personeller
                    .AsNoTracking()
                    .Where(p => ids.Contains(p.Id) && p.BranchId == branchId && p.AktifMi && p.IsActive && !p.IsDeleted)
                    .Select(p => new { p.Id, p.Ad, p.Soyad })
                    .ToListAsync();

                if (personeller.Count != ids.Count)
                    throw new InvalidOperationException("Seçilen kaza şahitlerinden biri aktif şubeye ait veya aktif değil.");

                await _context.IsKazasiSahitleri.AddRangeAsync(personeller.Select(p => new IsKazasiSahit
                {
                    IsKazasiId = isKazasiId,
                    PersonelId = p.Id,
                    AdSoyad = $"{p.Ad} {p.Soyad}".Trim(),
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false
                }));
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int branchId)
        {
            var isKazasi = await _context.IsKazalari
                .Include(i => i.Dosyalar)
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.BranchId == branchId);

            if (isKazasi == null)
                return;

            var diskPaths = isKazasi.Dosyalar
                .Select(d => d.DosyaYolu)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            _context.IsKazasiDosyalar.RemoveRange(isKazasi.Dosyalar);
            _context.IsKazalari.Remove(isKazasi);
            await _context.SaveChangesAsync();

            // DB silme başarılı olduktan sonra dosyaları temizle. Dosya sistemi hatası
            // veritabanındaki hard-delete işlemini geri çevirmesin.
            foreach (var path in diskPaths)
            {
                TryDeleteDosyaFromDisk(path);
            }
        }

        public async Task<PaginatedResult<IsKazasi>> GetIsKazalariAsync(IsKazasiFilterDto filter)
        {
            IQueryable<IsKazasi> query = _context.IsKazalari
                .AsNoTracking()
                .Where(i => !i.IsDeleted);

            if (filter.PageSize == int.MaxValue)
            {
                // Excel/PDF export bütün ilişkileri kullanır.
                query = query
                    .AsSplitQuery()
                    .Include(i => i.Personel)
                    .Include(i => i.Branch)
                    .Include(i => i.Department)
                    .Include(i => i.Unit)
                    .Include(i => i.Gorev)
                    .Include(i => i.Grup)
                .Include(i => i.Vardiya)
                    .Include(i => i.Dosyalar);
            }
            else
            {
                // Liste ekranı yalnız Şube ve Departman adını kullanır.
                query = query
                    .Include(i => i.Branch)
                    .Include(i => i.Department);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(i =>
                    (i.PersonelAdSoyad != null && i.PersonelAdSoyad.ToLower().Contains(search)) ||
                    (i.Aciklama != null && i.Aciklama.ToLower().Contains(search))
                );
            }

            if (filter.PersonelId.HasValue)
                query = query.Where(i => i.PersonelId == filter.PersonelId.Value);

            if (filter.BranchId.HasValue)
                query = query.Where(i => i.BranchId == filter.BranchId.Value);

            if (filter.DepartmentId.HasValue)
                query = query.Where(i => i.DepartmentId == filter.DepartmentId.Value);

            if (filter.KayipGunMin.HasValue)
                query = query.Where(i => i.KayipGun >= filter.KayipGunMin.Value);

            if (filter.KayipGunMax.HasValue)
                query = query.Where(i => i.KayipGun <= filter.KayipGunMax.Value);

            if (filter.BaslangicTarihi.HasValue)
                query = query.Where(i => i.KazaTarihi >= filter.BaslangicTarihi.Value);

            if (filter.BitisTarihi.HasValue)
                query = query.Where(i => i.KazaTarihi <= filter.BitisTarihi.Value);

            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                var sortBy = filter.SortBy.ToLower();
                if (sortBy == "kaza tarihi")
                    query = filter.SortDescending ? query.OrderByDescending(i => i.KazaTarihi) : query.OrderBy(i => i.KazaTarihi);
                else if (sortBy == "personel")
                    query = filter.SortDescending ? query.OrderByDescending(i => i.PersonelAdSoyad) : query.OrderBy(i => i.PersonelAdSoyad);
                else if (sortBy == "kayıp gün")
                    query = filter.SortDescending ? query.OrderByDescending(i => i.KayipGun) : query.OrderBy(i => i.KayipGun);
                else
                    query = filter.SortDescending ? query.OrderByDescending(i => i.CreatedDate) : query.OrderBy(i => i.KazaTarihi);
            }
            else
            {
                query = query.OrderByDescending(i => i.KazaTarihi);
            }

            if (filter.PageSize == int.MaxValue)
            {
                // Export akışında COUNT + aynı sorguyu tekrar çalıştırma maliyetini kaldır.
                var allItems = await query.ToListAsync();
                return new PaginatedResult<IsKazasi>
                {
                    Items = allItems,
                    TotalCount = allItems.Count,
                    PageNumber = 1,
                    PageSize = allItems.Count
                };
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<IsKazasi>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PersonelAutocompleteDto?> GetPersonelByIdAsync(int id, int branchId)
        {
            return await _context.Personeller
                .AsNoTracking()
                .Where(p => p.Id == id && p.BranchId == branchId && !p.IsDeleted && p.AktifMi)
                .Select(p => new PersonelAutocompleteDto
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    SicilNo = p.SicilNo,
                    Email = p.Email,
                    BranchId = p.BranchId,
                    BranchAdi = p.Branch != null ? p.Branch.BranchAdi : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentAdi = p.Department != null ? p.Department.DepartmentAdi : null,
                    UnitId = p.UnitId,
                    UnitAdi = p.Unit != null ? p.Unit.UnitAdi : null,
                    GorevId = p.GorevId,
                    GorevAdi = p.Gorev != null ? p.Gorev.Ad : null,
                    GrupId = p.GrupId,
                    GrupAdi = p.Grup != null ? p.Grup.Ad : null,
                    DogumTarihi = p.DogumTarihi,
                    IseGirisTarihi = p.IseGirisTarihi
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<PersonelAutocompleteDto>> SearchPersonelAsync(string searchTerm, int branchId)
        {
            var term = searchTerm?.Trim();
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2 || branchId <= 0)
                return Array.Empty<PersonelAutocompleteDto>();

            // %term% tüm tablo taraması oluşturur. Büyük personel tablolarında prefix arama
            // indekslerden yararlanabildiği için çok daha ölçeklenebilirdir.
            var parts = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var first = parts[0];
            var second = parts.Length > 1 ? parts[1] : null;

            var query = _context.Personeller
                .AsNoTracking()
                .Where(p => p.AktifMi && p.IsActive && !p.IsDeleted && p.BranchId == branchId);

            if (!string.IsNullOrWhiteSpace(second))
            {
                query = query.Where(p =>
                    (p.Ad.StartsWith(first) && p.Soyad.StartsWith(second)) ||
                    (p.Ad.StartsWith(second) && p.Soyad.StartsWith(first)) ||
                    p.SicilNo.StartsWith(term) ||
                    p.Email.StartsWith(term));
            }
            else
            {
                query = query.Where(p =>
                    p.Ad.StartsWith(first) ||
                    p.Soyad.StartsWith(first) ||
                    p.SicilNo.StartsWith(first) ||
                    p.Email.StartsWith(first));
            }

            return await query
                .OrderBy(p => p.Ad)
                .ThenBy(p => p.Soyad)
                .Take(15)
                .Select(p => new PersonelAutocompleteDto
                {
                    Id = p.Id,
                    FullName = p.Ad + " " + p.Soyad,
                    SicilNo = p.SicilNo,
                    Email = p.Email,
                    BranchId = p.BranchId,
                    BranchAdi = p.Branch != null ? p.Branch.BranchAdi : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentAdi = p.Department != null ? p.Department.DepartmentAdi : null,
                    UnitId = p.UnitId,
                    UnitAdi = p.Unit != null ? p.Unit.UnitAdi : null,
                    GorevId = p.GorevId,
                    GorevAdi = p.Gorev != null ? p.Gorev.Ad : null,
                    GrupId = p.GrupId,
                    GrupAdi = p.Grup != null ? p.Grup.Ad : null,
                    DogumTarihi = p.DogumTarihi,
                    IseGirisTarihi = p.IseGirisTarihi
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<IsKazasiDosya>> GetDosyalarAsync(int isKazasiId)
        {
            return await _context.IsKazasiDosyalar
                .AsNoTracking()
                .Where(d => d.IsKazasiId == isKazasiId)
                .ToListAsync();
        }

        public async Task<bool> DeleteDosyaAsync(int dosyaId, int branchId)
        {
            var dosya = await _context.IsKazasiDosyalar
                .Include(d => d.IsKazasi)
                .FirstOrDefaultAsync(d =>
                    d.Id == dosyaId &&
                    !d.IsDeleted &&
                    d.IsKazasi != null &&
                    d.IsKazasi.BranchId == branchId &&
                    !d.IsKazasi.IsDeleted);

            if (dosya == null)
                return false;

            var diskPath = dosya.DosyaYolu;
            _context.IsKazasiDosyalar.Remove(dosya);
            await _context.SaveChangesAsync();
            TryDeleteDosyaFromDisk(diskPath);
            return true;
        }

        private async Task SaveDosyalarAsync(int isKazasiId, List<IFormFile> dosyalar)
        {
            await AddDosyalarToContextAsync(isKazasiId, dosyalar);
            await _context.SaveChangesAsync();
        }

        private async Task AddDosyalarToContextAsync(int isKazasiId, List<IFormFile> dosyalar)
        {
            var validFiles = dosyalar
                .Where(file => file != null && file.Length > 0)
                .ToList();

            if (validFiles.Count == 0)
                return;

            var uploadsFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "is-kazalari",
                isKazasiId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            foreach (var file in validFiles)
            {
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var dosyaTipi = GetDosyaTipi(fileExtension);
                var fileName = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}"[..28] + fileExtension;
                var filePath = Path.Combine(uploadsFolder, fileName);
                var relativePath = $"/uploads/is-kazalari/{isKazasiId}/{fileName}";

                await using (var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    useAsync: true))
                {
                    await file.CopyToAsync(stream);
                }

                _context.IsKazasiDosyalar.Add(new IsKazasiDosya
                {
                    IsKazasiId = isKazasiId,
                    DosyaAdi = file.FileName,
                    DosyaYolu = relativePath,
                    DosyaTipi = dosyaTipi,
                    DosyaBoyutu = file.Length,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                });
            }
        }

        private void TryDeleteDosyaFromDisk(string? dosyaYolu)
        {
            if (string.IsNullOrWhiteSpace(dosyaYolu))
                return;

            try
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, dosyaYolu.TrimStart('/'));
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // DB hard-delete tamamlandıysa fiziksel dosya temizliği isteği başarısız
                // diye CRUD işlemini kullanıcıya başarısız göstermiyoruz.
            }
        }

        private string GetDosyaTipi(string extension)
        {
            return extension switch
            {
                ".pdf" => "pdf",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "image",
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" => "video",
                _ => "other"
            };
        }

        public async Task<IsKazasiRaporDto> GetRaporAsync(
            int? departmentId = null,
            int? branchId = null,
            int? onemliKazaGun = 3,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null)
        {
            var query = _context.IsKazalari
                .AsNoTracking()
                .Where(i => !i.IsDeleted);

            if (departmentId.HasValue)
                query = query.Where(i => i.DepartmentId == departmentId.Value);
            if (branchId.HasValue)
                query = query.Where(i => i.BranchId == branchId.Value);
            if (baslangicTarihi.HasValue)
                query = query.Where(i => i.KazaTarihi >= baslangicTarihi.Value.Date);
            if (bitisTarihi.HasValue)
            {
                var bitisHaric = bitisTarihi.Value.Date.AddDays(1);
                query = query.Where(i => i.KazaTarihi < bitisHaric);
            }

            // Rapor için yalnız gereken kolonları getir; bütün entity/navigation grafiğini belleğe taşıma.
            var kazalar = await query
                .Select(i => new
                {
                    i.DepartmentId,
                    DepartmentAdi = i.Department != null ? i.Department.DepartmentAdi : "Belirsiz",
                    i.KazaTarihi,
                    i.KayipGun,
                    i.RaporBaslangic,
                    i.RaporBitis
                })
                .ToListAsync();

            var tumDepartmanlar = await _context.Departments
                .AsNoTracking()
                .Where(d => !d.IsDeleted && (!branchId.HasValue || d.BranchId == branchId.Value))
                .OrderBy(d => d.DepartmentAdi)
                .Select(d => new { d.Id, d.DepartmentAdi })
                .ToListAsync();

            var tumAylar = Enumerable.Range(1, 12).ToArray();
            var ayAdlari = new[] { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            var onemliKazaGunDegeri = onemliKazaGun ?? 3;

            // Her departmanın kayıtlarını bir kez grupla; önceki yapıda aynı liste
            // departman x ay x matris sayısı kadar tekrar tekrar taranıyordu.
            var departmanGruplari = kazalar
                .GroupBy(k => k.DepartmentId ?? 0)
                .ToDictionary(g => g.Key, g => g.ToList());

            var kazaMatrisi = new List<DepartmanAyMatrisDto>(tumDepartmanlar.Count);
            var kayipGunMatrisi = new List<DepartmanAyMatrisDto>(tumDepartmanlar.Count);
            var onemliKazaMatrisi = new List<DepartmanAyMatrisDto>(tumDepartmanlar.Count);
            var devredenKayipGunMatrisi = new List<DepartmanAyMatrisDto>(tumDepartmanlar.Count);

            foreach (var dep in tumDepartmanlar)
            {
                departmanGruplari.TryGetValue(dep.Id, out var depKazalar);
                depKazalar ??= new();

                var aylikGruplar = depKazalar
                    .GroupBy(k => k.KazaTarihi.Month)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var kazaAylar = new Dictionary<int, AyMatrisData>(12);
                var kayipAylar = new Dictionary<int, AyMatrisData>(12);
                var onemliAylar = new Dictionary<int, AyMatrisData>(12);
                var devredenAylar = tumAylar.ToDictionary(ay => ay, _ => new AyMatrisData());

                foreach (var ay in tumAylar)
                {
                    aylikGruplar.TryGetValue(ay, out var ayKazalar);
                    ayKazalar ??= new();
                    kazaAylar[ay] = new AyMatrisData { Deger = ayKazalar.Count };
                    kayipAylar[ay] = new AyMatrisData { Deger = ayKazalar.Sum(k => k.KayipGun ?? 0) };
                    onemliAylar[ay] = new AyMatrisData { Deger = ayKazalar.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri) };
                }

                foreach (var kaza in depKazalar)
                {
                    if (!kaza.RaporBaslangic.HasValue || !kaza.RaporBitis.HasValue)
                        continue;

                    var raporBaslangic = kaza.RaporBaslangic.Value.Date;
                    var raporBitis = kaza.RaporBitis.Value.Date;

                    if (baslangicTarihi.HasValue && raporBaslangic < baslangicTarihi.Value.Date)
                        raporBaslangic = baslangicTarihi.Value.Date;
                    if (bitisTarihi.HasValue && raporBitis > bitisTarihi.Value.Date)
                        raporBitis = bitisTarihi.Value.Date;

                    if (raporBitis < raporBaslangic)
                        continue;

                    var kayipBaslangic = kaza.KazaTarihi.Date == raporBaslangic
                        ? raporBaslangic.AddDays(1)
                        : raporBaslangic;
                    if (kayipBaslangic > raporBitis)
                        continue;

                    var gun = kayipBaslangic;
                    while (gun <= raporBitis)
                    {
                        var aySonu = new DateTime(gun.Year, gun.Month, DateTime.DaysInMonth(gun.Year, gun.Month));
                        var parcaBitis = aySonu < raporBitis ? aySonu : raporBitis;
                        devredenAylar[gun.Month].Deger += (parcaBitis - gun).Days + 1;
                        gun = parcaBitis.AddDays(1);
                    }
                }

                kazaMatrisi.Add(new DepartmanAyMatrisDto
                {
                    DepartmentId = dep.Id,
                    DepartmentAdi = dep.DepartmentAdi,
                    Aylar = kazaAylar,
                    Toplam = depKazalar.Count
                });
                kayipGunMatrisi.Add(new DepartmanAyMatrisDto
                {
                    DepartmentId = dep.Id,
                    DepartmentAdi = dep.DepartmentAdi,
                    Aylar = kayipAylar,
                    Toplam = depKazalar.Sum(k => k.KayipGun ?? 0)
                });
                onemliKazaMatrisi.Add(new DepartmanAyMatrisDto
                {
                    DepartmentId = dep.Id,
                    DepartmentAdi = dep.DepartmentAdi,
                    Aylar = onemliAylar,
                    Toplam = depKazalar.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri)
                });
                devredenKayipGunMatrisi.Add(new DepartmanAyMatrisDto
                {
                    DepartmentId = dep.Id,
                    DepartmentAdi = dep.DepartmentAdi,
                    Aylar = devredenAylar,
                    Toplam = devredenAylar.Values.Sum(a => a.Deger)
                });
            }

            // İş kazası araştırma cevaplarını, rapor filtresiyle aynı kaza kümesi üzerinden
            // departman + kategori + madde bazında say. Bir kazada aynı maddeye birden fazla
            // cevap kaydı bulunsa bile hücrede kaza yalnızca bir kez sayılır.
            var hedefKategoriAnahtarlari = new[]
            {
                "birincil neden", "kok neden", "yaralanan bolge", "yaralanma turu", "yaralayici etken"
            };

            static string RaporMetniNormalize(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                return value.Trim().ToLowerInvariant()
                    .Replace("ı", "i").Replace("İ", "i")
                    .Replace("ş", "s").Replace("Ş", "s")
                    .Replace("ğ", "g").Replace("Ğ", "g")
                    .Replace("ü", "u").Replace("Ü", "u")
                    .Replace("ö", "o").Replace("Ö", "o")
                    .Replace("ç", "c").Replace("Ç", "c");
            }

            var arastirmaKategorileri = await _context.IsKazasiArastirmaKategorileri
                .AsNoTracking()
                .Where(k => !k.IsDeleted && k.IsActive)
                .OrderBy(k => k.Sira)
                .ThenBy(k => k.Ad)
                .Select(k => new
                {
                    k.Id,
                    k.Ad,
                    k.Sira,
                    Maddeler = k.Maddeler
                        .Where(m => !m.IsDeleted && m.IsActive)
                        .OrderBy(m => m.Sira)
                        .ThenBy(m => m.Id)
                        .Select(m => new { m.Id, m.Metin, m.Sira })
                        .ToList()
                })
                .ToListAsync();

            arastirmaKategorileri = arastirmaKategorileri
                .Where(k => hedefKategoriAnahtarlari.Any(h => RaporMetniNormalize(k.Ad).Contains(h)))
                .ToList();

            var hedefKategoriIdleri = arastirmaKategorileri.Select(k => k.Id).ToList();

            var cevapQuery = _context.IsKazasiArastirmaCevaplari
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.IsActive
                    && c.Madde != null
                    && hedefKategoriIdleri.Contains(c.Madde.KategoriId)
                    && c.Arastirma != null
                    && !c.Arastirma.IsDeleted
                    && c.Arastirma.IsActive
                    && c.Arastirma.IsKazasi != null
                    && !c.Arastirma.IsKazasi.IsDeleted);

            if (departmentId.HasValue)
                cevapQuery = cevapQuery.Where(c => c.Arastirma!.IsKazasi!.DepartmentId == departmentId.Value);
            if (branchId.HasValue)
                cevapQuery = cevapQuery.Where(c => c.Arastirma!.IsKazasi!.BranchId == branchId.Value);
            if (baslangicTarihi.HasValue)
                cevapQuery = cevapQuery.Where(c => c.Arastirma!.IsKazasi!.KazaTarihi >= baslangicTarihi.Value.Date);
            if (bitisTarihi.HasValue)
            {
                var bitisHaric = bitisTarihi.Value.Date.AddDays(1);
                cevapQuery = cevapQuery.Where(c => c.Arastirma!.IsKazasi!.KazaTarihi < bitisHaric);
            }

            var arastirmaSayimlari = await cevapQuery
                .Select(c => new
                {
                    KazaId = c.Arastirma!.IsKazasiId,
                    DepartmentId = c.Arastirma.IsKazasi!.DepartmentId,
                    MaddeId = c.MaddeId,
                    KategoriId = c.Madde!.KategoriId
                })
                .Distinct()
                .GroupBy(x => new { x.DepartmentId, x.KategoriId, x.MaddeId })
                .Select(g => new
                {
                    g.Key.DepartmentId,
                    g.Key.KategoriId,
                    g.Key.MaddeId,
                    KazaSayisi = g.Count()
                })
                .ToListAsync();

            var arastirmaKategoriMatrisleri = arastirmaKategorileri.Select(k =>
            {
                var maddeler = k.Maddeler.Select(m => new ArastirmaMaddeBaslikDto
                {
                    MaddeId = m.Id,
                    Metin = m.Metin,
                    Sira = m.Sira,
                    ToplamKaza = arastirmaSayimlari
                        .Where(x => x.KategoriId == k.Id && x.MaddeId == m.Id)
                        .Sum(x => x.KazaSayisi)
                }).ToList();

                var departmanlar = tumDepartmanlar.Select(d =>
                {
                    var hucreler = maddeler.ToDictionary(
                        m => m.MaddeId,
                        m => arastirmaSayimlari
                            .Where(x => x.KategoriId == k.Id && x.MaddeId == m.MaddeId && x.DepartmentId == d.Id)
                            .Sum(x => x.KazaSayisi));

                    return new ArastirmaDepartmanSatirDto
                    {
                        DepartmentId = d.Id,
                        DepartmentAdi = d.DepartmentAdi,
                        MaddeKazaSayilari = hucreler,
                        ToplamKaza = hucreler.Values.Sum()
                    };
                }).ToList();

                return new ArastirmaKategoriMatrisDto
                {
                    KategoriId = k.Id,
                    KategoriAdi = k.Ad,
                    Sira = k.Sira,
                    Maddeler = maddeler,
                    Departmanlar = departmanlar,
                    ToplamKaza = maddeler.Sum(m => m.ToplamKaza)
                };
            }).ToList();

            var rapor = new IsKazasiRaporDto
            {
                OnemliKazaGun = onemliKazaGunDegeri,
                ToplamKaza = kazalar.Count,
                ToplamKayipGun = kazalar.Sum(k => k.KayipGun ?? 0),
                OnemliKazaSayisi = kazalar.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri),
                OnemliKazaKayipGun = kazalar.Where(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri).Sum(k => k.KayipGun ?? 0),
                KazaMatrisi = kazaMatrisi,
                KayipGunMatrisi = kayipGunMatrisi,
                OnemliKazaMatrisi = onemliKazaMatrisi,
                DevredenKayipGunMatrisi = devredenKayipGunMatrisi,
                ArastirmaKategoriMatrisleri = arastirmaKategoriMatrisleri,
                AylikRaporlar = kazalar
                    .GroupBy(k => new { k.KazaTarihi.Year, k.KazaTarihi.Month })
                    .Select(g => new AylikRaporDto
                    {
                        Yil = g.Key.Year,
                        Ay = g.Key.Month,
                        ToplamKaza = g.Count(),
                        ToplamKayipGun = g.Sum(k => k.KayipGun ?? 0),
                        OnemliKazaSayisi = g.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri)
                    })
                    .OrderBy(a => a.Yil).ThenBy(a => a.Ay).ToList(),
                DepartmanRaporlari = departmanGruplari
                    .Select(pair =>
                    {
                        var g = pair.Value;
                        var monthly = g.GroupBy(k => k.KazaTarihi.Month).ToDictionary(x => x.Key, x => x.ToList());
                        return new DepartmanRaporDto
                        {
                            DepartmentId = pair.Key,
                            DepartmentAdi = g.FirstOrDefault()?.DepartmentAdi ?? "Belirsiz",
                            ToplamKaza = g.Count,
                            ToplamKayipGun = g.Sum(k => k.KayipGun ?? 0),
                            OnemliKazaSayisi = g.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri),
                            OnemliKazaKayipGun = g.Where(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri).Sum(k => k.KayipGun ?? 0),
                            AylikDetaylar = tumAylar.Select(ay =>
                            {
                                monthly.TryGetValue(ay, out var ayKazalar);
                                ayKazalar ??= new();
                                return new AyDetayDto
                                {
                                    Ay = ay,
                                    AyAdi = ayAdlari[ay - 1],
                                    KazaSayisi = ayKazalar.Count,
                                    KayipGun = ayKazalar.Sum(k => k.KayipGun ?? 0),
                                    OnemliKazaSayisi = ayKazalar.Count(k => (k.KayipGun ?? 0) >= onemliKazaGunDegeri)
                                };
                            }).ToList()
                        };
                    })
                    .OrderByDescending(d => d.ToplamKaza)
                    .ToList()
            };

            return rapor;
        }
    }
}
