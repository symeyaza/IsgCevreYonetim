using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922230000_AddTehlikeliIsCanvasSignatures")]
public class AddTehlikeliIsCanvasSignatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('TehlikeliIsGunleri', 'KontrolImzaYolu') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [KontrolImzaYolu] nvarchar(500) NULL;
            IF COL_LENGTH('TehlikeliIsGunleri', 'KontrolImzaSha256') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [KontrolImzaSha256] nvarchar(64) NULL;
            IF COL_LENGTH('TehlikeliIsGunleri', 'OnayImzaYolu') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [OnayImzaYolu] nvarchar(500) NULL;
            IF COL_LENGTH('TehlikeliIsGunleri', 'OnayImzaSha256') IS NULL ALTER TABLE [TehlikeliIsGunleri] ADD [OnayImzaSha256] nvarchar(64) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('TehlikeliIsGunleri', 'OnayImzaSha256') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [OnayImzaSha256];
            IF COL_LENGTH('TehlikeliIsGunleri', 'OnayImzaYolu') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [OnayImzaYolu];
            IF COL_LENGTH('TehlikeliIsGunleri', 'KontrolImzaSha256') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [KontrolImzaSha256];
            IF COL_LENGTH('TehlikeliIsGunleri', 'KontrolImzaYolu') IS NOT NULL ALTER TABLE [TehlikeliIsGunleri] DROP COLUMN [KontrolImzaYolu];
            """);
    }
}
