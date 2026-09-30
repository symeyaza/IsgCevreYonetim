using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace IsgCevreYonetim.Web.Filters
{
    /// <summary>
    /// Sayfa/işlem yetkisini ve aktif şube bağlamını tek noktadan uygular.
    /// Aktif şirket IUserScopeService tarafından personelin kendi şirketine sabitlenir.
    /// Aktif şube ise lokasyon seçme yetkisine göre kendi şubesi veya Dashboard'ta
    /// aynı şirket içerisinden seçilmiş şubedir.
    /// </summary>
    public class AuthorizationActionFilter : IAsyncActionFilter
    {
        private readonly IPagePermissionService _pagePermissionService;
        private readonly IUserScopeService _userScopeService;

        public AuthorizationActionFilter(
            IPagePermissionService pagePermissionService,
            IUserScopeService userScopeService)
        {
            _pagePermissionService = pagePermissionService;
            _userScopeService = userScopeService;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            var action = context.RouteData.Values["action"]?.ToString() ?? "Index";

            // Bildirim uçları yalnız giriş yapan kullanıcının kendi kayıtlarını döndürür;
            // ayrıca sayfa yetkisi gerektirmez.
            if (controller.Equals("Bildirim", StringComparison.OrdinalIgnoreCase) ||
                controller.Equals("Mesaj", StringComparison.OrdinalIgnoreCase) ||
                controller.Equals("MesajPush", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Heartbeat yalnızca oturum sahibinin kendi çevrimiçi kaydını günceller.
            if (controller.Equals("OnlineUsers", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("Ping", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Oturum yönetimi / Dashboard şube seçimi kendi kontrollerini yapar.
            if (controller.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                (action is "Index" or "Login" or "Logout" or "AccessDenied" or "Error" or "SetCompany" or "SetBranch" or "ClearBranch"))
            {
                await next();
                return;
            }

            var scope = await _userScopeService.GetAsync();
            if (scope == null)
            {
                Deny(context, "Kullanıcı şirket/şube kapsamı belirlenemedi.");
                return;
            }

            context.HttpContext.Items["CurrentCompanyId"] = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            context.HttpContext.Items["CurrentBranchId"] = scope.ActiveBranchId;
            context.HttpContext.Items["HomeBranchId"] = scope.HomeBranchId;
            context.HttpContext.Items["CanSelectBranch"] = scope.CanSelectBranch;

            // Süper Admin tüm CRUD işlemlerinde yetki matrisi kısıtı olmadan çalışır.
            // Operasyonel controller'lar yine Dashboard'ta seçilen şirket/şubeye sabitlenir;
            // Organizasyon, Yetki ve Personel yönetimi global hedeflerle çalışabilir.
            if (scope.IsSystemAdmin)
            {
                if (!controller.Equals("Yetki", StringComparison.OrdinalIgnoreCase) &&
                    !controller.Equals("Organization", StringComparison.OrdinalIgnoreCase) &&
                    !controller.Equals("Personel", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyBranchToArguments(context.ActionArguments, scope.ActiveBranchId);
                    ApplyCompanyToArguments(context.ActionArguments, scope.ActiveCompanyId);
                }

                await next();
                return;
            }

            // Bağımlı dropdown uçları tenant/şube scope'una ek olarak en az bir
            // ilgili sayfa yetkisi gerektirir. Böylece endpoint URL'si elle çağrılarak
            // yetkisiz organizasyon/personel verisi okunamaz.
            if (controller.Equals("Organization", StringComparison.OrdinalIgnoreCase) &&
                (action.Equals("BranchesByCompany", StringComparison.OrdinalIgnoreCase) ||
                 action.Equals("DepartmentsByBranch", StringComparison.OrdinalIgnoreCase) ||
                 action.Equals("UnitsByDepartment", StringComparison.OrdinalIgnoreCase)))
            {
                // Normal kullanıcı lookup istekleri aktif şirket/şubeye sabitlenir.
                // Süper Admin Personel Ekle/Güncelle ekranlarında global cascade kullanabildiği için
                // request'teki hedef Company/Branch değerleri korunur; controller hiyerarşiyi doğrular.
                if (!scope.IsSystemAdmin)
                {
                    ApplyBranchToArguments(context.ActionArguments, scope.ActiveBranchId);
                    ApplyCompanyToArguments(context.ActionArguments, scope.CompanyId);
                }

                var lookupPermissionBranchId = scope.IsSystemAdmin ? scope.HomeBranchId : scope.ActiveBranchId;
                var lookupAllowed = action switch
                {
                    "BranchesByCompany" => await HasAnyPermissionAsync(scope.PersonelId, lookupPermissionBranchId,
                        ("/Organization/Branches", "Goster"),
                        ("/Organization/Branches", "Ekle"),
                        ("/Organization/Branches", "Guncelle"),
                        ("/Personel/Index", "Goster"),
                        ("/Personel/Index", "Ekle"),
                        ("/Personel/Index", "Guncelle")),

                    "DepartmentsByBranch" => await HasAnyPermissionAsync(scope.PersonelId, lookupPermissionBranchId,
                        ("/Organization/Departments", "Goster"),
                        ("/Organization/Departments", "Ekle"),
                        ("/Organization/Departments", "Guncelle"),
                        ("/Personel/Index", "Goster"),
                        ("/Personel/Index", "Ekle"),
                        ("/Personel/Index", "Guncelle"),
                        ("/IsKazasi/Index", "Goster"),
                        ("/IsKazasi/Index", "Ekle"),
                        ("/IsKazasi/Index", "Guncelle")),

                    _ => await HasAnyPermissionAsync(scope.PersonelId, lookupPermissionBranchId,
                        ("/Organization/Units", "Goster"),
                        ("/Organization/Units", "Ekle"),
                        ("/Organization/Units", "Guncelle"),
                        ("/Personel/Index", "Goster"),
                        ("/Personel/Index", "Ekle"),
                        ("/Personel/Index", "Guncelle"),
                        ("/IsKazasi/Index", "Goster"),
                        ("/IsKazasi/Index", "Ekle"),
                        ("/IsKazasi/Index", "Guncelle"))
                };

                if (!lookupAllowed)
                {
                    Deny(context, "Bu veriyi görüntülemek için yetkiniz bulunmuyor.");
                    return;
                }

                await next();
                return;
            }

            if (controller.Equals("Reference", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("GetIlcelerByIlId", StringComparison.OrdinalIgnoreCase))
            {
                var lookupAllowed = await HasAnyPermissionAsync(scope.PersonelId, scope.ActiveBranchId,
                    ("/Reference/Ilceler", "Goster"),
                    ("/Reference/Ilceler", "Ekle"),
                    ("/Reference/Ilceler", "Guncelle"))
                    || await HasAnyPermissionAsync(scope.PersonelId, scope.HomeBranchId,
                        ("/Organization/Branches", "Ekle"),
                        ("/Organization/Branches", "Guncelle"));

                if (!lookupAllowed)
                {
                    Deny(context, "İlçe verilerini görüntülemek için yetkiniz bulunmuyor.");
                    return;
                }

                await next();
                return;
            }

            if (controller.Equals("IsKazasi", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("SearchPersonel", StringComparison.OrdinalIgnoreCase))
            {
                var lookupAllowed = await HasAnyPermissionAsync(scope.PersonelId, scope.ActiveBranchId,
                    ("/IsKazasi/Index", "Goster"),
                    ("/IsKazasi/Index", "Ekle"),
                    ("/IsKazasi/Index", "Guncelle"));

                if (!lookupAllowed)
                {
                    Deny(context, "Personel araması için yetkiniz bulunmuyor.");
                    return;
                }

                await next();
                return;
            }

            // Yetki ekranında branchId yöneticinin yetki tanımladığı hedef şubedir.
            // Organizasyon master CRUD da Dashboard scope'una bağlı değildir; request'teki
            // CompanyId/BranchId/DepartmentId değerleri Süper Admin'in yönettiği hedef kayıtlardır.
            // Diğer operasyonel ekranlarda request scope aktif şubeye zorlanır.
            if (!controller.Equals("Yetki", StringComparison.OrdinalIgnoreCase) &&
                !controller.Equals("Organization", StringComparison.OrdinalIgnoreCase) &&
                !(scope.IsSystemAdmin && controller.Equals("Personel", StringComparison.OrdinalIgnoreCase)))
            {
                ApplyBranchToArguments(context.ActionArguments, scope.ActiveBranchId);
                ApplyCompanyToArguments(context.ActionArguments, scope.CompanyId);
            }

            var operation = GetOperation(action, context.HttpContext.Request.Method);
            var pageUrl = GetPermissionPageUrl(controller, action);

            // Layout ve action filter aynı scoped PagePermissionService örneğini kullanır.
            // İzinler request başına tek SQL sorgusunda topluca yüklendiğinden burada
            // tekrar Personel/YetkiSayfa sorgusu çalışmaz.
            var authorized = await _pagePermissionService.CanAsync(pageUrl, operation);

            if (!authorized)
            {
                Deny(context, "Bu işlem için yetkiniz bulunmuyor.");
                return;
            }

            await next();
        }

        private async Task<bool> HasAnyPermissionAsync(
            int personelId,
            int branchId,
            params (string PageUrl, string Operation)[] permissions)
        {
            // personelId/branchId imzası mevcut çağrıları bozmamak için korunuyor.
            // PagePermissionService organizasyon/home branch ve operasyonel/active branch
            // ayrımını merkezi olarak uygular ve sonuçları request boyunca cache'ler.
            foreach (var permission in permissions)
            {
                if (await _pagePermissionService.CanAsync(permission.PageUrl, permission.Operation))
                    return true;
            }

            return false;
        }

        private static void Deny(ActionExecutingContext context, string message)
        {
            var request = context.HttpContext.Request;

            var isAjax = string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            var acceptsJson = request.Headers.Accept.Any(x =>
                x != null && x.Contains("application/json", StringComparison.OrdinalIgnoreCase));

            if (isAjax || acceptsJson)
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }

            context.Result = new RedirectToActionResult(
                "AccessDenied",
                "Home",
                new { message });
        }

        private static string GetOperation(string action, string method)
        {
            if (action.StartsWith("Dosya", StringComparison.OrdinalIgnoreCase) && action.Contains("Sil", StringComparison.OrdinalIgnoreCase)) return "Guncelle";
            if (action.Contains("Sil", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Delete", StringComparison.OrdinalIgnoreCase))
                return "Sil";

            if (action.Contains("Ekle", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Create", StringComparison.OrdinalIgnoreCase))
                return "Ekle";

            if (action.Contains("Duzenle", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Guncelle", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("Yetkilendir", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("GunKaydet", StringComparison.OrdinalIgnoreCase))
                return "Guncelle";

            return "Goster";
        }

        private static string GetPermissionPageUrl(string controller, string action)
        {
            if (controller.Equals("Organization", StringComparison.OrdinalIgnoreCase))
            {
                if (action.Equals("Companies", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Company", StringComparison.OrdinalIgnoreCase))
                    return "/Organization/Companies";

                if (action.Equals("Branches", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("BranchesByCompany", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Branch", StringComparison.OrdinalIgnoreCase))
                    return "/Organization/Branches";

                if (action.Equals("Departments", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("DepartmentsByBranch", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Department", StringComparison.OrdinalIgnoreCase))
                    return "/Organization/Departments";

                if (action.Equals("Units", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("UnitsByDepartment", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Unit", StringComparison.OrdinalIgnoreCase))
                    return "/Organization/Units";
            }

            if (controller.Equals("Reference", StringComparison.OrdinalIgnoreCase))
            {
                if (action.Equals("Iller", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlEkle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlDuzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlSil", StringComparison.OrdinalIgnoreCase))
                    return "/Reference/Iller";

                if (action.Equals("Ilceler", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlceEkle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlceDuzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("IlceSil", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("GetIlcelerByIlId", StringComparison.OrdinalIgnoreCase))
                    return "/Reference/Ilceler";

                if (action.Equals("Gorevler", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Gorev", StringComparison.OrdinalIgnoreCase))
                    return "/Reference/Gorevler";

                if (action.Equals("Gruplar", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Grup", StringComparison.OrdinalIgnoreCase))
                    return "/Reference/Gruplar";

                if (action.Equals("Cinsiyetler", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Cinsiyet", StringComparison.OrdinalIgnoreCase))
                    return "/Reference/Cinsiyetler";
            }

            if (controller.Equals("Personel", StringComparison.OrdinalIgnoreCase))
            {
                if (action.Equals("Rapor", StringComparison.OrdinalIgnoreCase) || action.Equals("RaporExcel", StringComparison.OrdinalIgnoreCase))
                    return "/Personel/Rapor";
                if (action.Equals("Index", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Ekle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Duzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Detay", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Sil", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("ProfilResmiSil", StringComparison.OrdinalIgnoreCase))
                    return "/Personel/Index";
            }

            if (controller.Equals("MudahaleSekli", StringComparison.OrdinalIgnoreCase))
                return "/MudahaleSekli/Index";

            if (controller.Equals("TehlikeliIs", StringComparison.OrdinalIgnoreCase))
                return action.Equals("Rapor", StringComparison.OrdinalIgnoreCase) || action.Equals("RaporExcel", StringComparison.OrdinalIgnoreCase)
                    ? "/TehlikeliIs/Rapor" : "/TehlikeliIs/Index";

            if (controller.Equals("Taseron", StringComparison.OrdinalIgnoreCase))
                return "/Taseron/Index";

            if (controller.Equals("TehlikeSinifi", StringComparison.OrdinalIgnoreCase))
                return "/TehlikeSinifi/Index";

            if (controller.Equals("IsDurumu", StringComparison.OrdinalIgnoreCase))
                return "/IsDurumu/Index";

            if (controller.Equals("IsKazasi", StringComparison.OrdinalIgnoreCase))
            {
                // Raporlama bağımsız bir sayfa/yetki satırıdır. Rapor ekranı ve
                // rapor dışa aktarımları bu satırın Görüntüle yetkisini kullanır.
                if (action.Equals("Rapor", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("ExportExcel", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("ExportPdf", StringComparison.OrdinalIgnoreCase))
                    return "/IsKazasi/Rapor";

                if (action.Equals("Index", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Ekle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Duzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Detay", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Sil", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("DosyaSil", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SearchPersonel", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("ExcelIndir", StringComparison.OrdinalIgnoreCase))
                    return "/IsKazasi/Index";
            }

            if (controller.Equals("Cevre", StringComparison.OrdinalIgnoreCase))
            {
                if (action.StartsWith("Tur", StringComparison.OrdinalIgnoreCase)) return "/Cevre/Turler";
                if (action.StartsWith("FirmaTur", StringComparison.OrdinalIgnoreCase)) return "/Cevre/FirmaTurleri";
                if (action.StartsWith("Firma", StringComparison.OrdinalIgnoreCase)) return "/Cevre/Firmalar";
                if (action.StartsWith("Atik", StringComparison.OrdinalIgnoreCase)) return "/Cevre/Atiklar";
                if (action.StartsWith("Takip", StringComparison.OrdinalIgnoreCase) || action.StartsWith("Dosya", StringComparison.OrdinalIgnoreCase)) return "/Cevre/Takip";
                return "/Cevre/Index";
            }

            if (controller.Equals("Vardiya", StringComparison.OrdinalIgnoreCase))
            {
                // Vardiya CRUD işlemlerinin tamamı tek yetkilendirme satırından yönetilir.
                // Index = Görüntüle, Ekle = Ekle, Duzenle = Güncelle, Sil = Sil.
                return "/Vardiya/Index";
            }

            if (controller.Equals("DashboardGuncelleme", StringComparison.OrdinalIgnoreCase))
            {
                // Listeleme ve CRUD işlemleri tek yetkilendirme satırından yönetilir.
                return "/DashboardGuncelleme/Index";
            }

            if (controller.Equals("IsKazasiDuzelticiFaaliyet", StringComparison.OrdinalIgnoreCase))
            {
                // Düzeltici faaliyetler tek bir yetki satırı üzerinden CRUD kontrol edilir.
                // Index/Detay = Görüntüle, Ekle = Ekle, Duzenle = Güncelle, Sil/DosyaSil = Sil.
                return "/IsKazasiDuzelticiFaaliyet/Index";
            }

            if (controller.Equals("IsKazasiArastirma", StringComparison.OrdinalIgnoreCase))
            {
                if (action.Equals("Kategoriler", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Kategori", StringComparison.OrdinalIgnoreCase))
                    return "/IsKazasiArastirma/Kategoriler";

                if (action.Equals("Maddeler", StringComparison.OrdinalIgnoreCase) ||
                    action.StartsWith("Madde", StringComparison.OrdinalIgnoreCase))
                    return "/IsKazasiArastirma/Maddeler";

                if (action.Equals("Arastirma", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("ArastirmaGuncelle", StringComparison.OrdinalIgnoreCase))
                    return "/IsKazasiArastirma/Arastirma";
            }

            if (controller.Equals("Yetki", StringComparison.OrdinalIgnoreCase))
            {
                if (action.Equals("Yetkiler", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("YetkiEkle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("YetkiDuzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("YetkiSil", StringComparison.OrdinalIgnoreCase))
                    return "/Yetki/Yetkiler";

                if (action.Equals("Sayfalar", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SayfaEkle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SayfaDuzenle", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SayfaSil", StringComparison.OrdinalIgnoreCase))
                    return "/Yetki/Sayfalar";

                if (action.Equals("Index", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("Yetkilendir", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("GetYetkiSayfalar", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("GetYetkiSubeAyari", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("GetSayfalar", StringComparison.OrdinalIgnoreCase))
                    return "/Yetki/Index";
            }

            return $"/{controller}/{action}";
        }

        private static void ApplyBranchToArguments(
            IDictionary<string, object?> arguments,
            int branchId)
        {
            foreach (var key in arguments.Keys.ToList())
            {
                var value = arguments[key];

                if (key.Equals("branchId", StringComparison.OrdinalIgnoreCase))
                {
                    arguments[key] = branchId;
                    continue;
                }

                if (value == null)
                    continue;

                var prop = value.GetType().GetProperty(
                    "BranchId",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (prop?.CanWrite != true)
                    continue;

                if (prop.PropertyType == typeof(int))
                    prop.SetValue(value, branchId);
                else if (prop.PropertyType == typeof(int?))
                    prop.SetValue(value, (int?)branchId);
            }
        }

        private static void ApplyCompanyToArguments(
            IDictionary<string, object?> arguments,
            int companyId)
        {
            foreach (var key in arguments.Keys.ToList())
            {
                var value = arguments[key];

                // Organization/Branches gibi filtrelerde companyId request'ten değiştirilemesin.
                if (key.Equals("companyId", StringComparison.OrdinalIgnoreCase))
                {
                    arguments[key] = companyId;
                    continue;
                }

                if (value == null)
                    continue;

                var prop = value.GetType().GetProperty(
                    "CompanyId",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (prop?.CanWrite != true)
                    continue;

                if (prop.PropertyType == typeof(int))
                    prop.SetValue(value, companyId);
                else if (prop.PropertyType == typeof(int?))
                    prop.SetValue(value, (int?)companyId);
            }
        }
    }
}
