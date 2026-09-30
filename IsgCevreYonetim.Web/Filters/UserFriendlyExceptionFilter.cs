using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Filters
{
    /// <summary>
    /// CRUD sırasında oluşan beklenmeyen hataların geliştirici exception ekranı yerine
    /// kullanıcıya ortak modal/JSON mesajı olarak dönmesini sağlar.
    /// Teknik ayrıntı ILogger üzerinden kaydedilir.
    /// </summary>
    public sealed class UserFriendlyExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<UserFriendlyExceptionFilter> _logger;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public UserFriendlyExceptionFilter(
            ILogger<UserFriendlyExceptionFilter> logger,
            ITempDataDictionaryFactory tempDataFactory)
        {
            _logger = logger;
            _tempDataFactory = tempDataFactory;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(
                context.Exception,
                "İstek işlenirken hata oluştu. Path: {Path}",
                context.HttpContext.Request.Path);

            var message = GetUserMessage(context.Exception);
            var request = context.HttpContext.Request;

            var isAjax = string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            var acceptsJson = request.Headers.Accept.Any(x =>
                x != null &&
                x.Contains("application/json", StringComparison.OrdinalIgnoreCase));

            if (isAjax || acceptsJson)
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };

                context.ExceptionHandled = true;
                return;
            }

            var tempData = _tempDataFactory.GetTempData(context.HttpContext);
            tempData["ToastrError"] = message;

            var referer = request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectResult(uri.PathAndQuery);
            }
            else
            {
                context.Result = new RedirectToActionResult("Dashboard", "Home", null);
            }

            context.ExceptionHandled = true;
        }

        private static string GetUserMessage(Exception exception)
        {
            if (exception is DbUpdateException)
            {
                return "Veritabanı işlemi tamamlanamadı. Aynı kod/ad ile kayıt bulunabilir veya kayıt başka veriler tarafından kullanılıyor olabilir.";
            }

            if (exception is InvalidOperationException &&
                exception.Message.Contains("tracked", StringComparison.OrdinalIgnoreCase))
            {
                return "Kayıt güncellenirken veri takip çakışması oluştu. Lütfen sayfayı yenileyip tekrar deneyin.";
            }

            // Servis katmanındaki kontrollü iş kuralı mesajlarını kullanıcıya göster.
            // SQL/connection gibi çok uzun teknik mesajları doğrudan taşımıyoruz.
            var message = exception.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(message) &&
                message.Length <= 350 &&
                !message.Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("System.Data", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains(" at ", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            return "İşlem sırasında beklenmeyen bir hata oluştu. Lütfen işlemi tekrar deneyin.";
        }
    }
}
