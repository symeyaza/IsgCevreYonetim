using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Filters;

/// <summary>
/// Her isteği personelin tek yetkisi, seçili şube ve sayfa işlem izniyle doğrular.
/// Böylece yalnızca menü gizlemek yerine sunucu tarafında da yetki zorlanır.
/// </summary>
public sealed class PermissionFilter : IAsyncActionFilter
{
    private readonly IYetkiService _yetkiService;
    private readonly ApplicationDbContext _context;
    public PermissionFilter(IYetkiService yetkiService, ApplicationDbContext context)
    {
        _yetkiService = yetkiService;
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true ||
            context.Controller is not Controller controller)
        {
            await next();
            return;
        }

        var descriptor = context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
        var controllerName = descriptor?.ControllerName;
        if (string.Equals(controllerName, "Home", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptor?.ActionName, "AccessDenied", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (!int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var personelId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var pageUrl = $"/{controllerName}/{descriptor?.ActionName}";
        var operation = ResolveOperation(context.HttpContext.Request.Method, descriptor?.ActionName);
        int? branchId = null;
        if (int.TryParse(context.HttpContext.Session.GetString("SelectedBranchId"), out var selected))
            branchId = selected;
        if (!branchId.HasValue)
            branchId = await _context.Personeller.AsNoTracking()
                .Where(p => p.Id == personelId && !p.IsDeleted && p.AktifMi)
                .Select(p => p.BranchId).FirstOrDefaultAsync();

        foreach (var argument in context.ActionArguments)
        {
            if (argument.Key.Equals("branchId", StringComparison.OrdinalIgnoreCase) && branchId.HasValue)
                context.ActionArguments[argument.Key] = branchId.Value;
            else if (argument.Value != null && branchId.HasValue)
            {
                var property = argument.Value.GetType().GetProperty("BranchId");
                if (property?.CanWrite == true && property.PropertyType == typeof(int?))
                    property.SetValue(argument.Value, branchId);
                else if (property?.CanWrite == true && property.PropertyType == typeof(int))
                    property.SetValue(argument.Value, branchId.Value);
            }
        }

        if (!branchId.HasValue)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!await _yetkiService.KullaniciYetkiliMiAsync(
        personelId,
        pageUrl,
        operation,
        branchId.Value))
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private static string ResolveOperation(string method, string? action)
    {
        if (HttpMethods.IsGet(method)) return "Goster";
        if (action?.Contains("Sil", StringComparison.OrdinalIgnoreCase) == true) return "Sil";
        if (action?.Contains("Duzenle", StringComparison.OrdinalIgnoreCase) == true) return "Guncelle";
        return "Ekle";
    }
}
