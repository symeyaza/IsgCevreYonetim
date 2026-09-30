using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IsgCevreYonetim.Infrastructure.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = (Environment.GetEnvironmentVariable("ISG_DB_CONNECTION") ?? configuration.GetConnectionString("DefaultConnection"))
                ?? throw new InvalidOperationException("DefaultConnection bağlantı cümlesi bulunamadı.");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ISG_DB_CONNECTION veya ConnectionStrings:DefaultConnection tanımlanmalıdır.");

            // Uzak SQL sunucularında hosting/firewall/NAT nedeniyle havuzdaki fiziksel bağlantı
            // sunucu tarafından kapatılmış olabilir. SqlClient bağlantı dayanıklılığı ayarlarını
            // açıkça tanımlıyoruz. MARS bu projede gerekli değil ve Session Provider katmanını
            // gereksiz yere devreye sokabildiği için kapatılıyor.
            var connectionBuilder = new SqlConnectionStringBuilder(connectionString)
            {
                MultipleActiveResultSets = false,
                ConnectRetryCount = 5,
                ConnectRetryInterval = 2,
                ConnectTimeout = 20,
                // Uzak SQL bağlantılarını iki dakikada bir zorla emekliye ayırmak
                // aralıklı sayfa gecikmelerine neden olur. 0 = connection pool kendi
                // yaşam döngüsünü yönetsin; kopmuş bağlantılar EnableRetryOnFailure ile
                // yeniden kurulur.
                LoadBalanceTimeout = 0
            };

            // DbContext nesnesinin kurulma maliyetini azaltmak için context pooling kullanılır.
            // Bu, SqlClient connection pool'undan ayrıdır; her request'te yeni context grafiği
            // kurmak yerine temizlenmiş context örnekleri yeniden kullanılır.
            services.AddDbContextPool<ApplicationDbContext>(options =>
            {
                // Bazı modüller geçmişte elle yazılmış SQL migration ile eklendi ve eski
                // model snapshot'ında bulunmuyor. Pending model uyarısı bu migration'ları
                // engellemesin; her yeni şema değişimi için açık migration gerekir.
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                options.UseSqlServer(connectionBuilder.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName);
                    sqlOptions.CommandTimeout(30);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 6,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            });

            // Servisler
            services.AddScoped<IPersonelService, PersonelService>();
            services.AddScoped<IReferenceService, ReferenceService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IYetkiService, YetkiService>();

            // ⭐ İş Kazası Servisi
            services.AddScoped<IIsKazasiService, IsKazasiService>();
            services.AddScoped<IIsKazasiArastirmaService, IsKazasiArastirmaService>();

            return services;
        }
    }
}