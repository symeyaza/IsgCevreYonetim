using Microsoft.AspNetCore.Mvc;

namespace IsgCevreYonetim.Web.ViewComponents
{
    public class FilterFieldsViewComponent : ViewComponent
    {
        private static readonly HashSet<string> SpecializedViews = new(StringComparer.OrdinalIgnoreCase)
        {
            "Personel_Index",
            "Yetki_Yetkiler",
            "Yetki_Sayfalar",
            "IsKazasi_Index",
            "IsKazasi_Rapor",
            "TehlikeliIs_Index",
            "Reference_Ilceler"
        };

        public IViewComponentResult Invoke(string? controllerName = null, string? actionName = null)
        {
            controllerName ??= ViewContext.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            actionName ??= ViewContext.RouteData.Values["action"]?.ToString() ?? string.Empty;

            var viewName = $"{controllerName}_{actionName}";

            if (controllerName.Equals("Cevre", StringComparison.OrdinalIgnoreCase) &&
                (actionName is "Turler" or "FirmaTurleri" or "Firmalar" or "Atiklar" or "Takip"))
                return View("Cevre_Search");

            return SpecializedViews.Contains(viewName)
                ? View(viewName)
                : View("Default");
        }
    }
}
