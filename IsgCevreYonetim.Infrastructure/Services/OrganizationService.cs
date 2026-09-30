using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsgCevreYonetim.Infrastructure.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static int _cacheVersion;
        private static readonly MemoryCacheEntryOptions LookupCacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

        public OrganizationService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private string CacheKey(string key) => $"org:{Volatile.Read(ref _cacheVersion)}:{key}";
        private static void InvalidateCache() => Interlocked.Increment(ref _cacheVersion);

        // ---------- ŞİRKET ----------
        public async Task<IEnumerable<Company>> GetAllCompaniesAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("companies:all"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Companies.AsNoTracking()
                    .Where(c => !c.IsDeleted).OrderBy(c => c.CompanyAdi).ToListAsync();
            }) ?? new List<Company>();
        }

        public async Task<Company?> GetCompanyByIdAsync(int id)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"company:{id}"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            });
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            if (string.IsNullOrWhiteSpace(company.CompanyKodu))
            {
                company.CompanyKodu = await GenerateNextCodeAsync(
                    _context.Companies.AsNoTracking().Select(x => x.CompanyKodu),
                    "COMP");
            }

            company.CreatedDate = DateTime.Now;
            company.IsActive = true;
            await _context.Companies.AddAsync(company);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return company;
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            var existing = await _context.Companies
                .FirstOrDefaultAsync(x => x.Id == company.Id && !x.IsDeleted)
                ?? throw new Exception("Şirket bulunamadı.");

            existing.CompanyAdi = company.CompanyAdi;
            existing.VergiNo = company.VergiNo;
            existing.VergiDairesi = company.VergiDairesi;
            existing.Telefon = company.Telefon;
            existing.Email = company.Email;
            existing.Adres = company.Adres;
            existing.LogoUrl = company.LogoUrl;
            existing.IsActive = company.IsActive;
            existing.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteCompanyAsync(int id)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id);
            if (company == null) return;

            if (await _context.Branches.IgnoreQueryFilters().AnyAsync(b => b.CompanyId == id))
                throw new Exception("Bu şirkete bağlı şubeler bulunuyor. Önce şubeleri silmelisiniz.");

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> CompanyExistsAsync(string kod)
        {
            return await _context.Companies
                .AnyAsync(c => c.CompanyKodu == kod && !c.IsDeleted);
        }

        // ---------- ŞUBE ----------
        public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("branches:all"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.BranchAdi).ToListAsync();
            }) ?? new List<Branch>();
        }

        public async Task<IEnumerable<Branch>> GetBranchesByCompanyIdAsync(int companyId)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"branches:company:{companyId}"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Branches.AsNoTracking().Where(b => b.CompanyId == companyId && !b.IsDeleted).OrderBy(b => b.BranchAdi).ToListAsync();
            }) ?? new List<Branch>();
        }

        public async Task<Branch?> GetBranchByIdAsync(int id)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"branch:{id}"), async entry =>
            {
                entry.SetOptions(LookupCacheOptions);
                return await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
            });
        }

        public async Task<Branch> CreateBranchAsync(Branch branch)
        {
            if (string.IsNullOrEmpty(branch.BranchKodu))
            {
                branch.BranchKodu = await GenerateBranchCodeAsync();
            }

            branch.CreatedDate = DateTime.Now;
            branch.IsActive = true;
            await _context.Branches.AddAsync(branch);
            await _context.SaveChangesAsync();
            InvalidateCache();

            // Yeni şube için sayfa/CRUD yetkileri burada otomatik verilmez.
            // Tek yetki kaynağı Yetkilendirme ekranındaki YetkiSayfa / YetkiSube kayıtlarıdır.
            // Böylece bir profil yeni şubede ancak açıkça yetkilendirildiyse işlem yapabilir.

            return branch;
        }

        public async Task UpdateBranchAsync(Branch branch)
        {
            var existing = await _context.Branches
                .FirstOrDefaultAsync(x => x.Id == branch.Id && !x.IsDeleted)
                ?? throw new Exception("Şube bulunamadı.");

            existing.BranchAdi = branch.BranchAdi;
            existing.CompanyId = branch.CompanyId;
            existing.SgkIsyeriSicilNo = branch.SgkIsyeriSicilNo;
            existing.SgkIsyeriKodu = branch.SgkIsyeriKodu;
            existing.VergiNo = branch.VergiNo;
            existing.VergiDairesi = branch.VergiDairesi;
            existing.Telefon = branch.Telefon;
            existing.Email = branch.Email;
            existing.Adres = branch.Adres;
            existing.PostaKodu = branch.PostaKodu;
            existing.YetkiliKisi = branch.YetkiliKisi;
            existing.YetkiliTelefon = branch.YetkiliTelefon;
            existing.IlId = branch.IlId;
            existing.IlceId = branch.IlceId;
            existing.IsActive = branch.IsActive;
            existing.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteBranchAsync(int id)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id);
            if (branch == null) return;

            if (await _context.Departments.IgnoreQueryFilters().AnyAsync(d => d.BranchId == id))
                throw new Exception("Bu şubeye bağlı departmanlar bulunuyor. Önce departmanları silmelisiniz.");
            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.BranchId == id))
                throw new Exception("Bu şubeye bağlı personeller bulunuyor. Önce personelleri başka şubeye taşımalı veya silmelisiniz.");
            if (await _context.IsKazalari.IgnoreQueryFilters().AnyAsync(x => x.BranchId == id))
                throw new Exception("Bu şubeye bağlı iş kazası kayıtları bulunuyor. Şube silinemez.");

            var permissionRows = await _context.YetkiSayfalar.IgnoreQueryFilters()
                .Where(x => x.BranchId == id).ToListAsync();
            var branchPermissionRows = await _context.YetkiSubeler.IgnoreQueryFilters()
                .Where(x => x.BranchId == id).ToListAsync();

            _context.YetkiSayfalar.RemoveRange(permissionRows);
            _context.YetkiSubeler.RemoveRange(branchPermissionRows);
            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> BranchExistsAsync(string kod)
        {
            return await _context.Branches
                .IgnoreQueryFilters()
                .AnyAsync(b => b.BranchKodu == kod && !b.IsDeleted);
        }

        public Task<string> GenerateBranchCodeAsync()
        {
            return GenerateNextCodeAsync(
                _context.Branches.AsNoTracking().Select(x => x.BranchKodu),
                "BRNCH");
        }

        // ---------- DEPARTMAN ----------
        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("departments:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Departments.AsNoTracking().Where(d => !d.IsDeleted).OrderBy(d => d.DepartmentAdi).ToListAsync(); }) ?? new List<Department>();
        }

        public async Task<IEnumerable<Department>> GetDepartmentsByBranchIdAsync(int branchId)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"departments:branch:{branchId}"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Departments.AsNoTracking().Where(d => d.BranchId == branchId && !d.IsDeleted).OrderBy(d => d.DepartmentAdi).ToListAsync(); }) ?? new List<Department>();
        }

        public async Task<Department?> GetDepartmentByIdAsync(int id)
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Branch)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        }

        public async Task<Department> CreateDepartmentAsync(Department department)
        {
            if (string.IsNullOrEmpty(department.DepartmentKodu))
            {
                department.DepartmentKodu = await GenerateDepartmentCodeAsync();
            }

            department.CreatedDate = DateTime.Now;
            department.IsActive = true;
            await _context.Departments.AddAsync(department);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return department;
        }

        public async Task UpdateDepartmentAsync(Department department)
        {
            var existing = await _context.Departments
                .FirstOrDefaultAsync(x => x.Id == department.Id && !x.IsDeleted)
                ?? throw new Exception("Departman bulunamadı.");

            existing.DepartmentAdi = department.DepartmentAdi;
            existing.BranchId = department.BranchId;
            existing.IsActive = department.IsActive;
            existing.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteDepartmentAsync(int id)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (department == null) return;

            if (await _context.Units.IgnoreQueryFilters().AnyAsync(u => u.DepartmentId == id))
                throw new Exception("Bu departmana bağlı birimler bulunuyor. Önce birimleri silmelisiniz.");
            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.DepartmentId == id))
                throw new Exception("Bu departmana bağlı personeller bulunuyor. Önce personellerin departmanını değiştirmelisiniz.");
            if (await _context.IsKazalari.IgnoreQueryFilters().AnyAsync(x => x.DepartmentId == id))
                throw new Exception("Bu departmana bağlı iş kazası kayıtları bulunuyor. Departman silinemez.");

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> DepartmentExistsAsync(string kod)
        {
            return await _context.Departments
                .IgnoreQueryFilters()
                .AnyAsync(d => d.DepartmentKodu == kod && !d.IsDeleted);
        }

        public Task<string> GenerateDepartmentCodeAsync()
        {
            return GenerateNextCodeAsync(
                _context.Departments.AsNoTracking().Select(x => x.DepartmentKodu),
                "DEP");
        }

        // ---------- BİRİM ----------
        public async Task<IEnumerable<Unit>> GetAllUnitsAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey("units:all"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Units.AsNoTracking().Where(u => !u.IsDeleted).OrderBy(u => u.UnitAdi).ToListAsync(); }) ?? new List<Unit>();
        }

        public async Task<IEnumerable<Unit>> GetUnitsByDepartmentIdAsync(int departmentId)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"units:department:{departmentId}"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Units.AsNoTracking().Where(u => u.DepartmentId == departmentId && !u.IsDeleted).OrderBy(u => u.UnitAdi).ToListAsync(); }) ?? new List<Unit>();
        }

        public async Task<IEnumerable<Unit>> GetUnitsByBranchIdAsync(int branchId)
        {
            return await _cache.GetOrCreateAsync(CacheKey($"units:branch:{branchId}"), async entry => { entry.SetOptions(LookupCacheOptions); return await _context.Units.AsNoTracking().Where(u => u.Department.BranchId == branchId && !u.IsDeleted).OrderBy(u => u.UnitAdi).ToListAsync(); }) ?? new List<Unit>();
        }

        public async Task<Unit?> GetUnitByIdAsync(int id)
        {
            return await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        }

        public async Task<Unit> CreateUnitAsync(Unit unit)
        {
            if (string.IsNullOrEmpty(unit.UnitKodu))
            {
                unit.UnitKodu = await GenerateUnitCodeAsync();
            }

            unit.CreatedDate = DateTime.Now;
            unit.IsActive = true;
            await _context.Units.AddAsync(unit);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return unit;
        }

        public async Task UpdateUnitAsync(Unit unit)
        {
            var existing = await _context.Units
                .FirstOrDefaultAsync(x => x.Id == unit.Id && !x.IsDeleted)
                ?? throw new Exception("Birim bulunamadı.");

            existing.UnitAdi = unit.UnitAdi;
            existing.DepartmentId = unit.DepartmentId;
            existing.IsActive = unit.IsActive;
            existing.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task DeleteUnitAsync(int id)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id);
            if (unit == null) return;

            if (await _context.Personeller.IgnoreQueryFilters().AnyAsync(p => p.UnitId == id))
                throw new Exception("Bu birime bağlı personeller bulunuyor. Önce personellerin birimini değiştirmelisiniz.");
            if (await _context.IsKazalari.IgnoreQueryFilters().AnyAsync(x => x.UnitId == id))
                throw new Exception("Bu birime bağlı iş kazası kayıtları bulunuyor. Birim silinemez.");

            _context.Units.Remove(unit);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<bool> UnitExistsAsync(string kod)
        {
            return await _context.Units
                .IgnoreQueryFilters()
                .AnyAsync(u => u.UnitKodu == kod && !u.IsDeleted);
        }

        public Task<string> GenerateUnitCodeAsync()
        {
            return GenerateNextCodeAsync(
                _context.Units.AsNoTracking().Select(x => x.UnitKodu),
                "UNIT");
        }

        private static async Task<string> GenerateNextCodeAsync(
            IQueryable<string?> codeQuery,
            string prefix)
        {
            var codes = await codeQuery
                .Where(x => x != null && x.StartsWith(prefix))
                .ToListAsync();

            var max = 0;
            foreach (var code in codes)
            {
                if (code == null || code.Length <= prefix.Length)
                    continue;

                if (int.TryParse(code[prefix.Length..], out var number) && number > max)
                    max = number;
            }

            return $"{prefix}{max + 1:D3}";
        }

        // ---------- SAYFALAMA ----------
        public async Task<PaginatedResult<Company>> GetCompaniesAsync(CompanyFilterDto filter)
        {
            var query = _context.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (filter.CompanyId.HasValue)
            {
                query = query.Where(c => c.Id == filter.CompanyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(c =>
                    c.CompanyAdi.ToLower().Contains(search) ||
                    c.CompanyKodu.ToLower().Contains(search) ||
                    (c.VergiNo != null && c.VergiNo.ToLower().Contains(search)) ||
                    (c.Email != null && c.Email.ToLower().Contains(search))
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "adi" => filter.SortDescending ? query.OrderByDescending(c => c.CompanyAdi) : query.OrderBy(c => c.CompanyAdi),
                "kod" => filter.SortDescending ? query.OrderByDescending(c => c.CompanyKodu) : query.OrderBy(c => c.CompanyKodu),
                "vergi" => filter.SortDescending ? query.OrderByDescending(c => c.VergiNo) : query.OrderBy(c => c.VergiNo),
                "tarih" => filter.SortDescending ? query.OrderByDescending(c => c.CreatedDate) : query.OrderBy(c => c.CreatedDate),
                _ => filter.SortDescending ? query.OrderByDescending(c => c.CreatedDate) : query.OrderBy(c => c.CompanyAdi)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Company>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Branch>> GetBranchesAsync(BranchFilterDto filter)
        {
            var query = _context.Branches
                .AsNoTracking()
                .Include(b => b.Company)
                .Include(b => b.Il)
                .Include(b => b.Ilce)
                .Where(b => !b.IsDeleted);

            if (filter.CompanyId.HasValue)
            {
                query = query.Where(b => b.CompanyId == filter.CompanyId.Value);
            }

            if (filter.BranchId.HasValue)
            {
                query = query.Where(b => b.Id == filter.BranchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(b =>
                    b.BranchAdi.ToLower().Contains(search) ||
                    b.BranchKodu.ToLower().Contains(search) ||
                    (b.VergiNo != null && b.VergiNo.ToLower().Contains(search)) ||
                    (b.Email != null && b.Email.ToLower().Contains(search))
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(b => b.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "adi" => filter.SortDescending ? query.OrderByDescending(b => b.BranchAdi) : query.OrderBy(b => b.BranchAdi),
                "kod" => filter.SortDescending ? query.OrderByDescending(b => b.BranchKodu) : query.OrderBy(b => b.BranchKodu),
                _ => filter.SortDescending ? query.OrderByDescending(b => b.CreatedDate) : query.OrderBy(b => b.BranchAdi)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Branch>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Department>> GetDepartmentsAsync(DepartmentFilterDto filter)
        {
            var query = _context.Departments
                .AsNoTracking()
                .Include(d => d.Branch)
                .ThenInclude(b => b.Company)
                .Where(d => !d.IsDeleted);

            if (filter.BranchId.HasValue)
            {
                query = query.Where(d => d.BranchId == filter.BranchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(d =>
                    d.DepartmentAdi.ToLower().Contains(search) ||
                    d.DepartmentKodu.ToLower().Contains(search)
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(d => d.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "adi" => filter.SortDescending ? query.OrderByDescending(d => d.DepartmentAdi) : query.OrderBy(d => d.DepartmentAdi),
                "kod" => filter.SortDescending ? query.OrderByDescending(d => d.DepartmentKodu) : query.OrderBy(d => d.DepartmentKodu),
                _ => filter.SortDescending ? query.OrderByDescending(d => d.CreatedDate) : query.OrderBy(d => d.DepartmentAdi)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Department>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PaginatedResult<Unit>> GetUnitsAsync(UnitFilterDto filter)
        {
            var query = _context.Units
                .AsNoTracking()
                .Include(u => u.Department)
                .ThenInclude(d => d.Branch)
                .Where(u => !u.IsDeleted);

            if (filter.BranchId.HasValue)
            {
                query = query.Where(u => u.Department.BranchId == filter.BranchId.Value);
            }

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == filter.DepartmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.UnitAdi.ToLower().Contains(search) ||
                    u.UnitKodu.ToLower().Contains(search)
                );
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "adi" => filter.SortDescending ? query.OrderByDescending(u => u.UnitAdi) : query.OrderBy(u => u.UnitAdi),
                "kod" => filter.SortDescending ? query.OrderByDescending(u => u.UnitKodu) : query.OrderBy(u => u.UnitKodu),
                _ => filter.SortDescending ? query.OrderByDescending(u => u.CreatedDate) : query.OrderBy(u => u.UnitAdi)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PaginatedResult<Unit>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }
    }
}