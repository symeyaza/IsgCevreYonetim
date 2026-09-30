using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsgCevreYonetim.Web.Services
{
    public sealed class PagePermissionInfo
    {
        public bool Goster { get; init; }
        public bool Ekle { get; init; }
        public bool Guncelle { get; init; }
        public bool Sil { get; init; }
    }

    public interface IPagePermissionService
    {
        Task<PagePermissionInfo> GetAsync(string pageUrl);
        Task<bool> CanAsync(string pageUrl, string operation);
        void InvalidateAll();
    }

    /// <summary>
    /// Bir HTTP request'i boyunca bütün sayfa izinlerini tek SQL sorgusunda yükler.
    /// Layout, action filter ve view'lar aynı scoped servis örneğini kullandığı için
    /// her sayfa için ayrı ayrı YetkiSayfa sorgusu çalışmaz.
    /// </summary>
    public sealed class PagePermissionService : IPagePermissionService
    {
        private readonly IUserScopeService _scopeService;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static long _permissionVersion;
        private readonly Dictionary<string, PagePermissionInfo> _resultCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<(int BranchId, string Url), PagePermissionInfo>? _permissionMap;
        private UserScopeInfo? _scope;
        private bool _loaded;

        public PagePermissionService(IUserScopeService scopeService, ApplicationDbContext context, IMemoryCache cache)
        {
            _scopeService = scopeService;
            _context = context;
            _cache = cache;
        }

        // Servis scoped olduğu için bu metod yalnız mevcut request içindeki sonuçları temizler.
        // Kalıcı/process cache kullanılmadığından sonraki request her zaman güncel DB verisini okur.
        public void InvalidateAll()
        {
            Interlocked.Increment(ref _permissionVersion);
            _loaded = false;
            _scope = null;
            _permissionMap = null;
            _resultCache.Clear();
        }

        public async Task<PagePermissionInfo> GetAsync(string pageUrl)
        {
            pageUrl = NormalizeUrl(pageUrl);
            if (_resultCache.TryGetValue(pageUrl, out var cached))
                return cached;

            await EnsureLoadedAsync();
            if (_scope == null || _permissionMap == null)
            {
                var empty = new PagePermissionInfo();
                _resultCache[pageUrl] = empty;
                return empty;
            }

            // Süper Admin için sayfa bazlı CRUD kısıtı yoktur.
            if (_scope.IsSystemAdmin)
            {
                var full = new PagePermissionInfo
                {
                    Goster = true,
                    Ekle = true,
                    Guncelle = true,
                    Sil = true
                };
                _resultCache[pageUrl] = full;
                return full;
            }

            var isOrganization = pageUrl.StartsWith("/Organization/", StringComparison.OrdinalIgnoreCase);
            var isResearchDefinition =
                pageUrl.Equals("/IsKazasiArastirma/Kategoriler", StringComparison.OrdinalIgnoreCase) ||
                pageUrl.Equals("/IsKazasiArastirma/Maddeler", StringComparison.OrdinalIgnoreCase);

            // Kategori/madde tanımları master veridir; Dashboard'ta seçilen aktif şubeye göre
            // yönetim yetkisinin değişmemesi için home branch yetkisi kullanılır.
            // Kaza bazlı Araştırma ekranı ise operasyoneldir ve aktif şube yetkisini kullanır.
            var permissionBranchId = (isOrganization || isResearchDefinition || _scope.IsSystemAdmin)
                ? _scope.HomeBranchId
                : _scope.ActiveBranchId;

            var direct = GetFromMap(permissionBranchId, pageUrl);

            // Organizasyon master yönetiminde Şirketler izni alt organizasyon
            // ekranları için fallback olarak kullanılmaya devam eder.
            PagePermissionInfo result;
            if (isOrganization &&
                !pageUrl.Equals("/Organization/Companies", StringComparison.OrdinalIgnoreCase))
            {
                var master = GetFromMap(_scope.HomeBranchId, "/Organization/Companies");
                result = Merge(direct, master);
            }
            else
            {
                result = direct;
            }

            _resultCache[pageUrl] = result;
            return result;
        }

        public async Task<bool> CanAsync(string pageUrl, string operation)
        {
            var permission = await GetAsync(pageUrl);
            return operation switch
            {
                "Ekle" => permission.Ekle,
                "Guncelle" => permission.Guncelle,
                "Sil" => permission.Sil,
                "Goster" => permission.Goster,
                _ => false
            };
        }

        private async Task EnsureLoadedAsync()
        {
            if (_loaded)
                return;

            _loaded = true;
            _scope = await _scopeService.GetAsync();
            if (_scope == null)
            {
                _permissionMap = new Dictionary<(int BranchId, string Url), PagePermissionInfo>();
                return;
            }

            if (_scope.IsSystemAdmin)
            {
                _permissionMap = new Dictionary<(int BranchId, string Url), PagePermissionInfo>();
                return;
            }

            var branchIds = _scope.HomeBranchId == _scope.ActiveBranchId
                ? new[] { _scope.HomeBranchId }
                : new[] { _scope.HomeBranchId, _scope.ActiveBranchId };

            // Uzak SQL sunucusunda her sayfa geçişinde aynı yetki matrisini tekrar okumak
            // ciddi ağ gecikmesi oluşturuyordu. Matris process belleğinde kısa süre tutulur.
            // YetkiController değişiklik yaptığında InvalidateAll sürümü artırdığı için cache anında boşa düşer.
            var version = Volatile.Read(ref _permissionVersion);
            var cacheKey = $"pageperm:{version}:{_scope.YetkiId}:{string.Join('-', branchIds.OrderBy(x => x))}";
            _permissionMap = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(2));
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                var rows = await _context.YetkiSayfalar
                    .AsNoTracking()
                    .Where(x => x.YetkiId == _scope.YetkiId && branchIds.Contains(x.BranchId) &&
                                x.IsActive && !x.IsDeleted && x.Sayfa.IsActive && !x.Sayfa.IsDeleted)
                    .Select(x => new { x.BranchId, Url = x.Sayfa.Url, x.Goster, x.Ekle, x.Guncelle, x.Sil })
                    .ToListAsync();
                return rows.GroupBy(x => (x.BranchId, Url: NormalizeUrl(x.Url)))
                    .ToDictionary(g => g.Key, g => new PagePermissionInfo
                    {
                        Goster = g.Any(x => x.Goster), Ekle = g.Any(x => x.Ekle),
                        Guncelle = g.Any(x => x.Guncelle), Sil = g.Any(x => x.Sil)
                    });
            }) ?? new Dictionary<(int BranchId, string Url), PagePermissionInfo>();
        }

        private PagePermissionInfo GetFromMap(int branchId, string pageUrl)
        {
            if (_permissionMap != null &&
                _permissionMap.TryGetValue((branchId, NormalizeUrl(pageUrl)), out var permission))
            {
                return permission;
            }

            return new PagePermissionInfo();
        }

        private static PagePermissionInfo Merge(PagePermissionInfo first, PagePermissionInfo second)
            => new()
            {
                Goster = first.Goster || second.Goster,
                Ekle = first.Ekle || second.Ekle,
                Guncelle = first.Guncelle || second.Guncelle,
                Sil = first.Sil || second.Sil
            };

        private static string NormalizeUrl(string? pageUrl)
            => ("/" + (pageUrl ?? string.Empty).Trim().Trim('/')).ToLowerInvariant();
    }
}
