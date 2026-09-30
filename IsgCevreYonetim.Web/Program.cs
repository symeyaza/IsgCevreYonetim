using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Infrastructure.Extensions;
using IsgCevreYonetim.Infrastructure.Seed;
using IsgCevreYonetim.Web.Filters;
using IsgCevreYonetim.Web.Services;
using IsgCevreYonetim.Web.Hubs;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// MVC
// ======================================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MessageAttachmentStorage>();
builder.Services.AddScoped<IUserScopeService, UserScopeService>();
builder.Services.AddScoped<IsKazasiExcelService>();
builder.Services.AddScoped<IPagePermissionService, PagePermissionService>();
builder.Services.AddScoped<AuthorizationActionFilter>();
builder.Services.AddScoped<UserFriendlyExceptionFilter>();
builder.Services.AddScoped<IBildirimService, BildirimService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MessagePushKeys>();
builder.Services.AddSingleton<MessagePushQueue>();
builder.Services.AddHostedService<MessagePushWorker>();
builder.Services.AddSingleton<OnlineConnectionTracker>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<AuthorizationActionFilter>();
    options.Filters.AddService<UserFriendlyExceptionFilter>();

    // Dinamik MVC/JSON cevapları tarayıcı/proxy cache'inden gösterilmesin.
    // CRUD işleminden sonraki ilk request mutlaka güncel veriyi sunar.
    options.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true,
        Location = ResponseCacheLocation.None
    });
});

// ======================================================
// INFRASTRUCTURE
// DbContext, servisler vb.
// ======================================================

builder.Services.AddInfrastructure(builder.Configuration);

// Authentication ve aktif şube cookie'lerinin IIS recycle sonrasında da çözülebilmesi için
// Data Protection anahtarları uygulama klasöründe kalıcı tutulur.
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("IsgCevreYonetim");

// ======================================================
// AUTHENTICATION - COOKIE
//
// NOT:
// Data Protection için manuel klasör tanımlamıyoruz.
// Hosting yazma/değiştirme izni vermediği için
// PersistKeysToFileSystem kullanılmıyor.
// ======================================================

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Giriş yapılmamış kullanıcı buraya yönlendirilir
        options.LoginPath = "/Home/Index";

        // Çıkış action'ı
        options.LogoutPath = "/Home/Logout";

        // Yetkisi olmayan kullanıcı
        options.AccessDeniedPath = "/Home/AccessDenied";

        // Authentication cookie adı
        options.Cookie.Name = "IsgCevreYonetim.Auth";

        // Güvenlik
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Uygulamanız HTTPS çalıştığı için
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Cookie ömrü
        options.ExpireTimeSpan = TimeSpan.FromDays(14);

        // Kullanıcı aktif oldukça sürenin yenilenmesini sağlar
        options.SlidingExpiration = true;
    });

// ======================================================
// SESSION
//
// Session login kaynağı değildir.
// PersonelId, ad, soyad vb. Claims üzerinden tutulmalı.
//
// Session yalnız:
// - SelectedBranchId
// - geçici filtre
// - geçici UI seçimleri
// gibi bilgiler için kullanılmalı.
// ======================================================

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);

    options.Cookie.Name = "IsgCevreYonetim.Session";

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ======================================================
// APPLICATION
// ======================================================

var app = builder.Build();

// ======================================================
// ERROR HANDLING
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// ======================================================
// HTTPS
// ======================================================

app.UseHttpsRedirection();

// HTML/JSON/CSS/JS yanıtlarını HTTPS üzerinde sıkıştırır.
app.UseResponseCompression();

// ======================================================
// STATIC FILES
// ======================================================

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new FileExtensionContentTypeProvider {
        Mappings = { [".webmanifest"] = "application/manifest+json" }
    },
    OnPrepareResponse = ctx =>
    {
        // Silinebilen kullanıcı yüklemeleri tarayıcıda/proxy üzerinde tutulmasın.
        var isPushAsset = string.Equals(ctx.Context.Request.Path.Value, "/mesaj-push-sw.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ctx.Context.Request.Path.Value, "/manifest.webmanifest", StringComparison.OrdinalIgnoreCase);
        ctx.Context.Response.Headers.CacheControl = isPushAsset || ctx.File.PhysicalPath?.Contains(
            $"{Path.DirectorySeparatorChar}uploads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) == true
            ? "private,no-store" : "public,max-age=604800";
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});

// ======================================================
// YAVAŞ REQUEST GÖZLEMİ
// ======================================================

app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    // Hosting ortamında aralıklı yavaşlayan endpoint'leri uygulama logundan
    // doğrudan görebilmek için yalnız 1 saniyeyi aşan istekleri işaretle.
    if (sw.ElapsedMilliseconds >= 1000)
    {
        app.Logger.LogWarning(
            "Yavaş istek: {Method} {Path} => {StatusCode} ({ElapsedMs} ms)",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
});

// ======================================================
// ROUTING
// ======================================================

app.UseRouting();

// ======================================================
// SESSION
//
// Authentication'dan önce devreye girsin.
// ======================================================

app.UseSession();

// ======================================================
// AUTHENTICATION
// ======================================================

app.UseAuthentication();

// ======================================================
// AUTHORIZATION
// ======================================================

app.UseAuthorization();

// ======================================================
// ROUTES
// ======================================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<BildirimHub>("/hubs/bildirim");
app.MapHub<OnlineUsersHub>("/hubs/online-users");
app.MapHub<MesajHub>("/hubs/mesaj");

// ======================================================
// DATABASE / SEED
// ======================================================
// Production'da her IIS recycle sırasında Migrate + Seed çalıştırmak hem açılışı yavaşlatır
// hem de kullanıcının sildiği seed kayıtlarını yeniden oluşturabilir. Bakım yalnız açıkça
// istendiğinde çalışır (ilk kurulum / kontrollü deployment).
if (builder.Configuration.GetValue<bool>("Database:RunStartupMaintenance"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<
            IsgCevreYonetim.Infrastructure.Data.ApplicationDbContext>();

        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(services);
        await YetkiSeeder.SeedAsync(context, app.Configuration);

        Console.WriteLine("Veritabanı migration/seed bakımı tamamlandı.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Veritabanı bakım hatası: {ex.Message}");
        throw;
    }
}

// ======================================================
// START
// ======================================================

app.Run();
