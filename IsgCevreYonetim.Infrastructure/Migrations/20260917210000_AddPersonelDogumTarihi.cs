using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260917210000_AddPersonelDogumTarihi")]
    public partial class AddPersonelDogumTarihi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski/canli veritabanlarinda kolon daha once elle eklenmis olabilir.
            // Bu nedenle migration tekrar calistiginda mevcut kolona dokunmaz.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Personeller', 'DogumTarihi') IS NULL
BEGIN
    ALTER TABLE [dbo].[Personeller] ADD [DogumTarihi] datetime2 NULL;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Personeller', 'DogumTarihi') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Personeller] DROP COLUMN [DogumTarihi];
END");
        }
    }
}
