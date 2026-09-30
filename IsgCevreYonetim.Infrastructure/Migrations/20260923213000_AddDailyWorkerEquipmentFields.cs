using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923213000_AddDailyWorkerEquipmentFields")]
public class AddDailyWorkerEquipmentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ALTER TABLE ile eklenen kolonlar, aynı SQL batch'i içindeki UPDATE
        // derlenirken henüz görünmez. Komutları ayrı migration operasyonları
        // olarak çalıştırarak SQL Server'ın her adımda şemayı yeniden derlemesini sağla.
        migrationBuilder.Sql("""
        IF COL_LENGTH('TehlikeliIsler','TasaronFirmaUnvani') IS NULL ALTER TABLE [TehlikeliIsler] ADD [TasaronFirmaUnvani] nvarchar(200) NULL;
        IF COL_LENGTH('TehlikeliIsGunleri','TasaronFirmaUnvani') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [TasaronFirmaUnvani] nvarchar(200) NULL;
        IF COL_LENGTH('TehlikeliIsGunleri','VincPlakasi') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [VincPlakasi] nvarchar(50) NULL;
        IF COL_LENGTH('TehlikeliIsGunleri','OperatorAdiSoyadi') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [OperatorAdiSoyadi] nvarchar(200) NULL;
        """);

        migrationBuilder.Sql("""
        UPDATE i SET [TasaronFirmaUnvani]=(SELECT TOP 1 k.[Firma] FROM [TehlikeliIsKisileri] k WHERE k.[TehlikeliIsId]=i.[Id] AND k.[Firma] IS NOT NULL ORDER BY k.[Id])
        FROM [TehlikeliIsler] i WHERE i.[TasaronFirmaUnvani] IS NULL;
        """);

        migrationBuilder.Sql("""
        UPDATE g SET [TasaronFirmaUnvani]=i.[TasaronFirmaUnvani] FROM [TehlikeliIsGunleri] g JOIN [TehlikeliIsler] i ON i.[Id]=g.[TehlikeliIsId] WHERE g.[TasaronFirmaUnvani] IS NULL;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH('TehlikeliIsGunleri','OperatorAdiSoyadi') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [OperatorAdiSoyadi];
        IF COL_LENGTH('TehlikeliIsGunleri','VincPlakasi') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [VincPlakasi];
        IF COL_LENGTH('TehlikeliIsGunleri','TasaronFirmaUnvani') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [TasaronFirmaUnvani];
        IF COL_LENGTH('TehlikeliIsler','TasaronFirmaUnvani') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [TasaronFirmaUnvani];
        """);
}
