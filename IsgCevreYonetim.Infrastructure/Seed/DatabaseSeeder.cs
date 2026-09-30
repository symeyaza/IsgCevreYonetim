using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IsgCevreYonetim.Infrastructure.Seed
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            // Migration Program.cs tarafında bir kez uygulanır. Burada tekrar EnsureCreated
            // çağırmak özellikle uzak SQL sunucusunda gereksiz bağlantı/metadata maliyeti yaratıyordu.
            await PersonelSeeder.SeedAsync(dbContext, configuration);
        }
    }
}
