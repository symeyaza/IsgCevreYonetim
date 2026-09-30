using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace IsgCevreYonetim.Web.Services
{
    /// <summary>
    /// Giriş yapan kullanıcının operasyonel kapsamını request başına bir kez hesaplar.
    /// Normal kullanıcı kendi şirketine sabittir. Yalnız "Süper Admin" yetkisine sahip
    /// kullanıcı Dashboard üzerinden şirket ve şube seçerek global kapsamda çalışabilir.
    /// </summary>
    public class UserScopeService : IUserScopeService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _selectionProtector;
        private readonly IMemoryCache _cache;
        private const string SelectionCookieName = "IsgCevreYonetim.ActiveScope";

        private UserScopeInfo? _cachedScope;
        private bool _initialized;

        public UserScopeService(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            IMemoryCache cache)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _selectionProtector = dataProtectionProvider.CreateProtector("IsgCevreYonetim.ActiveScope.v1");
            _cache = cache;
        }

        public async Task<UserScopeInfo?> GetAsync()
        {
            if (_initialized)
                return _cachedScope;

            _initialized = true;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
                return null;

            var personelIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(personelIdValue, out var personelId))
                return null;

            // Önce yalnız kimlik + Yetki okunur. Süper Admin'in Company/Branch/Department/Unit
            // bağlantısı olmak zorunda değildir. Normal kullanıcıda organizasyon zorunluluğu
            // aşağıda ayrıca doğrulanır.
            // Kimlik/organizasyon/yetki temeli sayfa geçişlerinde değişmez; uzak SQL'e her
            // request'te aynı SELECT'i göndermek yerine kısa süreli process cache kullan.
            // Kısa TTL, kullanıcı pasifleştirme/yetki değişikliklerinin hızla yansımasını korur.
            var baseScope = await _cache.GetOrCreateAsync($"userscope:base:{personelId}", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(20));
                return await ExecuteReadWithReconnectRetryAsync(() =>
                _context.Personeller
                    .AsNoTracking()
                    .Where(p =>
                        p.Id == personelId &&
                        !p.IsDeleted &&
                        p.AktifMi &&
                        p.IsActive &&
                        p.YetkiId != null &&
                        p.Yetki != null &&
                        p.Yetki.IsActive &&
                        !p.Yetki.IsDeleted)
                    .Select(p => new ScopeBaseProjection
                    {
                        Id = p.Id,
                        CompanyId = p.CompanyId,
                        HomeBranchId = p.BranchId,
                        YetkiId = p.YetkiId!.Value,
                        YetkiAdi = p.Yetki!.Ad,
                        CompanyName = p.Company != null ? p.Company.CompanyAdi : null,
                        HomeBranchName = p.Branch != null ? p.Branch.BranchAdi : null,
                        CompanyIsActive = p.Company != null && p.Company.IsActive && !p.Company.IsDeleted,
                        BranchIsActive = p.Branch != null && p.Branch.IsActive && !p.Branch.IsDeleted,
                        BranchCompanyId = p.Branch != null ? p.Branch.CompanyId : (int?)null,
                        CanSelectHomeBranch = p.BranchId != null && p.Yetki!.YetkiSubeler.Any(x =>
                            x.BranchId == p.BranchId.Value &&
                            x.LokasyonSecebilir &&
                            x.IsActive &&
                            !x.IsDeleted)
                    })
                    .FirstOrDefaultAsync());
            });

            if (baseScope == null)
                return null;

            var isSystemAdmin = string.Equals(
                baseScope.YetkiAdi,
                "Süper Admin",
                StringComparison.OrdinalIgnoreCase);

            // Normal kullanıcı mutlaka geçerli bir şirkete ve şubeye bağlıdır.
            if (!isSystemAdmin &&
                (baseScope.CompanyId == null ||
                 baseScope.HomeBranchId == null ||
                 !baseScope.CompanyIsActive ||
                 !baseScope.BranchIsActive ||
                 baseScope.BranchCompanyId != baseScope.CompanyId))
            {
                return null;
            }

            var homeCompanyId = isSystemAdmin ? 0 : baseScope.CompanyId!.Value;
            var homeBranchId = isSystemAdmin ? 0 : baseScope.HomeBranchId!.Value;
            var companyName = isSystemAdmin ? "Sistem Geneli" : (baseScope.CompanyName ?? string.Empty);
            var homeBranchName = isSystemAdmin ? "Organizasyondan Bağımsız" : (baseScope.HomeBranchName ?? string.Empty);

            if (isSystemAdmin)
            {
                // Süper Admin tamamen globaldir. Kalıcı Personel.CompanyId / BranchId dikkate alınmaz.
                // Operasyonel kapsam sadece Dashboard'ta seçilen şirket + şubeden gelir.
                var activeCompanyId = 0;
                var activeCompanyName = "Şirket seçilmedi";
                var activeBranchId = 0;
                var activeBranchName = "Şube seçilmedi";

                if (TryGetSelection(httpContext, out var selectedCompanyId, out _))
                {
                    var selectedCompany = await ExecuteReadWithReconnectRetryAsync(() =>
                        _context.Companies
                            .AsNoTracking()
                            .Where(c => c.Id == selectedCompanyId && c.IsActive && !c.IsDeleted)
                            .Select(c => new { c.Id, c.CompanyAdi })
                            .FirstOrDefaultAsync());

                    if (selectedCompany != null)
                    {
                        activeCompanyId = selectedCompany.Id;
                        activeCompanyName = selectedCompany.CompanyAdi;
                    }
                    else
                    {
                        ClearSelection(httpContext);
                    }
                }

                // İlk girişte seçim yoksa ilk aktif şirketi yalnız başlangıç kolaylığı için seç.
                if (activeCompanyId == 0)
                {
                    var firstCompany = await ExecuteReadWithReconnectRetryAsync(() =>
                        _context.Companies
                            .AsNoTracking()
                            .Where(c => c.IsActive && !c.IsDeleted)
                            .OrderBy(c => c.CompanyAdi)
                            .Select(c => new { c.Id, c.CompanyAdi })
                            .FirstOrDefaultAsync());

                    if (firstCompany != null)
                    {
                        activeCompanyId = firstCompany.Id;
                        activeCompanyName = firstCompany.CompanyAdi;
                        SetSelection(httpContext, firstCompany.Id, null);
                    }
                }

                if (activeCompanyId > 0 &&
                    TryGetSelection(httpContext, out _, out var selectedBranchId))
                {
                    var selectedBranch = await ExecuteReadWithReconnectRetryAsync(() =>
                        _context.Branches
                            .AsNoTracking()
                            .Where(b =>
                                b.Id == selectedBranchId &&
                                b.CompanyId == activeCompanyId &&
                                b.IsActive &&
                                !b.IsDeleted)
                            .Select(b => new BranchProjection { Id = b.Id, BranchAdi = b.BranchAdi })
                            .FirstOrDefaultAsync());

                    if (selectedBranch != null)
                    {
                        activeBranchId = selectedBranch.Id;
                        activeBranchName = selectedBranch.BranchAdi;
                    }
                    else
                    {
                        SetSelection(httpContext, activeCompanyId, null);
                    }
                }

                if (activeCompanyId > 0 && activeBranchId == 0)
                {
                    var firstBranch = await GetFirstActiveBranchAsync(activeCompanyId);
                    if (firstBranch != null)
                    {
                        activeBranchId = firstBranch.Id;
                        activeBranchName = firstBranch.BranchAdi;
                        SetSelection(httpContext, activeCompanyId, firstBranch.Id);
                    }
                }

                _cachedScope = new UserScopeInfo
                {
                    PersonelId = baseScope.Id,
                    CompanyId = 0,
                    HomeBranchId = 0,
                    ActiveCompanyId = activeCompanyId,
                    ActiveBranchId = activeBranchId,
                    YetkiId = baseScope.YetkiId,
                    CanSelectCompany = true,
                    CanSelectBranch = true,
                    IsSystemAdmin = true,
                    CompanyName = "Sistem Geneli",
                    ActiveCompanyName = activeCompanyName,
                    HomeBranchName = "Organizasyondan Bağımsız",
                    ActiveBranchName = activeBranchName
                };

                return _cachedScope;
            }

            // Lokasyon seçme bilgisi baseScope sorgusundaki EXISTS alt sorgusuyla zaten
            // alındı. Böylece normal kullanıcı için her request'te ikinci bir SQL sorgusu yok.
            var canSelectBranch = baseScope.CanSelectHomeBranch;

            var activeBranchIdNormal = homeBranchId;
            var activeBranchNameNormal = homeBranchName;

            if (canSelectBranch &&
                TryGetSelection(httpContext, out _, out var normalSelectedBranchId) &&
                normalSelectedBranchId != homeBranchId)
            {
                var selectedBranch = await ExecuteReadWithReconnectRetryAsync(() =>
                    _context.Branches
                        .AsNoTracking()
                        .Where(b =>
                            b.Id == normalSelectedBranchId &&
                            b.CompanyId == homeCompanyId &&
                            b.IsActive &&
                            !b.IsDeleted &&
                            b.Company != null &&
                            b.Company.IsActive &&
                            !b.Company.IsDeleted)
                        .Select(b => new { b.Id, b.BranchAdi })
                        .FirstOrDefaultAsync());

                if (selectedBranch != null)
                {
                    activeBranchIdNormal = selectedBranch.Id;
                    activeBranchNameNormal = selectedBranch.BranchAdi;
                }
                else
                {
                    SetSelection(httpContext, homeCompanyId, null);
                }
            }
            else if (!canSelectBranch)
            {
                SetSelection(httpContext, homeCompanyId, null);
            }

            // Active company normal kullanıcıda claim/DB kapsamından gelir; cookie yalnız aktif seçim için kullanılır.

            _cachedScope = new UserScopeInfo
            {
                PersonelId = baseScope.Id,
                CompanyId = homeCompanyId,
                HomeBranchId = homeBranchId,
                ActiveCompanyId = homeCompanyId,
                ActiveBranchId = activeBranchIdNormal,
                YetkiId = baseScope.YetkiId,
                CanSelectCompany = false,
                CanSelectBranch = canSelectBranch,
                IsSystemAdmin = false,
                CompanyName = companyName,
                ActiveCompanyName = companyName,
                HomeBranchName = homeBranchName,
                ActiveBranchName = activeBranchNameNormal
            };

            return _cachedScope;
        }

        public async Task<bool> IsBranchInCurrentCompanyAsync(int branchId)
        {
            var scope = await GetAsync();
            if (scope == null)
                return false;

            if (branchId == scope.ActiveBranchId)
                return true;

            var companyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            return await _context.Branches
                .AsNoTracking()
                .AnyAsync(b =>
                    b.Id == branchId &&
                    b.CompanyId == companyId &&
                    b.IsActive &&
                    !b.IsDeleted &&
                    b.Company != null &&
                    b.Company.IsActive &&
                    !b.Company.IsDeleted);
        }

        public async Task<bool> IsActiveBranchAsync(int branchId)
        {
            var scope = await GetAsync();
            return scope != null && scope.ActiveBranchId == branchId;
        }

        public async Task<bool> TrySetActiveCompanyAsync(int companyId)
        {
            var scope = await GetAsync();
            var httpContext = _httpContextAccessor.HttpContext;

            if (scope == null || httpContext == null || !scope.IsSystemAdmin)
                return false;

            var company = await _context.Companies
                .AsNoTracking()
                .Where(c => c.Id == companyId && c.IsActive && !c.IsDeleted)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync();

            if (company == null)
                return false;

            var firstBranch = await GetFirstActiveBranchAsync(companyId);

            // Süper Admin şubesi olmayan yeni bir şirketi de Dashboard'ta seçebilmelidir.
            // Bu durumda şirket aktif kapsam olur, şube seçimi boş kalır. İlk şube
            // oluşturulduğunda OrganizationController bu yeni şubeyi aktif şube yapar.
            SetSelection(httpContext, companyId, firstBranch?.Id);

            ResetCache();
            return true;
        }

        public async Task<bool> TrySetActiveBranchAsync(int branchId)
        {
            var scope = await GetAsync();
            var httpContext = _httpContextAccessor.HttpContext;

            if (scope == null || httpContext == null || !scope.CanSelectBranch)
                return false;

            var companyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;

            var valid = await _context.Branches
                .AsNoTracking()
                .AnyAsync(b =>
                    b.Id == branchId &&
                    b.CompanyId == companyId &&
                    b.IsActive &&
                    !b.IsDeleted &&
                    b.Company != null &&
                    b.Company.IsActive &&
                    !b.Company.IsDeleted);

            if (!valid)
                return false;

            SetSelection(httpContext, companyId, branchId);
            ResetCache();
            return true;
        }

        public void ClearSelectedCompany()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null) ClearSelection(httpContext);
            ResetCache();
        }

        public void ClearSelectedBranch()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var scope = _cachedScope;
                var companyId = scope?.IsSystemAdmin == true ? scope.ActiveCompanyId : scope?.CompanyId;
                SetSelection(httpContext, companyId > 0 ? companyId : null, null);
            }
            ResetCache();
        }

        private async Task<BranchProjection?> GetFirstActiveBranchAsync(int companyId)
        {
            return await ExecuteReadWithReconnectRetryAsync(() =>
                _context.Branches
                    .AsNoTracking()
                    .Where(b => b.CompanyId == companyId && b.IsActive && !b.IsDeleted)
                    .OrderBy(b => b.BranchAdi)
                    .Select(b => new BranchProjection { Id = b.Id, BranchAdi = b.BranchAdi })
                    .FirstOrDefaultAsync());
        }

        private bool TryGetSelection(HttpContext httpContext, out int companyId, out int branchId)
        {
            companyId = 0;
            branchId = 0;

            if (!httpContext.Request.Cookies.TryGetValue(SelectionCookieName, out var protectedValue) ||
                string.IsNullOrWhiteSpace(protectedValue))
                return false;

            try
            {
                var value = _selectionProtector.Unprotect(protectedValue);
                var parts = value.Split(':', 2);
                if (parts.Length != 2) return false;

                int.TryParse(parts[0], out companyId);
                int.TryParse(parts[1], out branchId);
                return companyId > 0 || branchId > 0;
            }
            catch
            {
                httpContext.Response.Cookies.Delete(SelectionCookieName);
                return false;
            }
        }

        private void SetSelection(HttpContext httpContext, int? companyId, int? branchId)
        {
            if ((!companyId.HasValue || companyId <= 0) && (!branchId.HasValue || branchId <= 0))
            {
                ClearSelection(httpContext);
                return;
            }

            var raw = $"{companyId.GetValueOrDefault()}:{branchId.GetValueOrDefault()}";
            var protectedValue = _selectionProtector.Protect(raw);
            httpContext.Response.Cookies.Append(SelectionCookieName, protectedValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(14)
            });
        }

        private static void ClearSelection(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(SelectionCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });
        }

        private void ResetCache()
        {
            _initialized = false;
            _cachedScope = null;
        }

        private async Task<T> ExecuteReadWithReconnectRetryAsync<T>(Func<Task<T>> operation)
        {
            try
            {
                return await operation();
            }
            catch (DbException)
            {
                try
                {
                    await _context.Database.CloseConnectionAsync();
                }
                catch
                {
                }

                await Task.Delay(100);
                return await operation();
            }
        }

        private sealed class ScopeBaseProjection
        {
            public int Id { get; init; }
            public int? CompanyId { get; init; }
            public int? HomeBranchId { get; init; }
            public int YetkiId { get; init; }
            public string? YetkiAdi { get; init; }
            public string? CompanyName { get; init; }
            public string? HomeBranchName { get; init; }
            public bool CompanyIsActive { get; init; }
            public bool BranchIsActive { get; init; }
            public int? BranchCompanyId { get; init; }
            public bool CanSelectHomeBranch { get; init; }
        }

        private sealed class BranchProjection
        {
            public int Id { get; init; }
            public string BranchAdi { get; init; } = string.Empty;
        }
    }
}
