using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922234500_AddTehlikeliIsMainSignatures")]
public class AddTehlikeliIsMainSignatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranImzaYolu') IS NULL ALTER TABLE [TehlikeliIsler] ADD [IsiYaptiranImzaYolu] nvarchar(500) NULL;
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranImzaSha256') IS NULL ALTER TABLE [TehlikeliIsler] ADD [IsiYaptiranImzaSha256] nvarchar(64) NULL;
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuImzaYolu') IS NULL ALTER TABLE [TehlikeliIsler] ADD [FirmaSorumlusuImzaYolu] nvarchar(500) NULL;
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuImzaSha256') IS NULL ALTER TABLE [TehlikeliIsler] ADD [FirmaSorumlusuImzaSha256] nvarchar(64) NULL;
        IF COL_LENGTH('TehlikeliIsKisileri', 'ImzaYolu') IS NULL ALTER TABLE [TehlikeliIsKisileri] ADD [ImzaYolu] nvarchar(500) NULL;
        IF COL_LENGTH('TehlikeliIsKisileri', 'ImzaSha256') IS NULL ALTER TABLE [TehlikeliIsKisileri] ADD [ImzaSha256] nvarchar(64) NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH('TehlikeliIsKisileri', 'ImzaSha256') IS NOT NULL ALTER TABLE [TehlikeliIsKisileri] DROP COLUMN [ImzaSha256];
        IF COL_LENGTH('TehlikeliIsKisileri', 'ImzaYolu') IS NOT NULL ALTER TABLE [TehlikeliIsKisileri] DROP COLUMN [ImzaYolu];
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuImzaSha256') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [FirmaSorumlusuImzaSha256];
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuImzaYolu') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [FirmaSorumlusuImzaYolu];
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranImzaSha256') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [IsiYaptiranImzaSha256];
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranImzaYolu') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [IsiYaptiranImzaYolu];
        """);
}
