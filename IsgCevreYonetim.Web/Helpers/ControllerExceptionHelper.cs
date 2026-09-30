using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Helpers;

/// <summary>
/// Controller içinde özellikle JSON/TempData ile cevaplanması gereken hatalarda
/// teknik exception bilgisinin istemciye sızmasını engeller ve ayrıntıyı sunucu loguna yazar.
/// </summary>
public static class ControllerExceptionHelper
{
    public static string GetSafeMessage(HttpContext httpContext, Exception exception)
    {
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ControllerException");
        logger.LogError(exception, "Controller işlemi başarısız. Path: {Path}", httpContext.Request.Path);

        if (exception is DbUpdateException || IsDatabaseException(exception))
        {
            return "Veritabanı işlemi tamamlanamadı. Kayıt başka veriler tarafından kullanılıyor olabilir veya aynı değer daha önce tanımlanmış olabilir.";
        }

        if (exception is InvalidOperationException or ArgumentException)
        {
            var message = exception.Message?.Trim();
            if (IsSafeBusinessMessage(message))
                return message!;
        }

        return "İşlem sırasında beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
    }

    private static bool IsDatabaseException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var fullName = current.GetType().FullName ?? string.Empty;
            if (fullName.StartsWith("Microsoft.Data.SqlClient.", StringComparison.Ordinal) ||
                fullName.StartsWith("System.Data.SqlClient.", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSafeBusinessMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 350)
            return false;

        string[] technicalMarkers =
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.SqlClient",
            "System.Data",
            "System.",
            "SELECT ",
            "INSERT ",
            "UPDATE ",
            "DELETE ",
            "ConnectionString",
            "Server=",
            "Password=",
            " at "
        };

        return !technicalMarkers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
