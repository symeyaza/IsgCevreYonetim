using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927232000_MoveAtikLisansNoToFirma")]
public class MoveAtikLisansNoToFirma : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Eski migration uygulanmışsa mevcut lisans numaralarını her firmaya aktar.
        // İlk kurulumda da önceki migration'ın hemen ardından çalışır.
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmalari', N'LisansNo') IS NULL
            ALTER TABLE [dbo].[AtikFirmalari] ADD [LisansNo] NVARCHAR(120) NULL;");
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmaTurleri', N'LisansNo') IS NOT NULL
            EXEC(N'UPDATE f SET f.LisansNo = t.LisansNo
                FROM dbo.AtikFirmalari AS f INNER JOIN dbo.AtikFirmaTurleri AS t ON t.Id = f.AtikFirmaTuruId
                WHERE f.LisansNo IS NULL AND t.LisansNo IS NOT NULL');");
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmaTurleri', N'LisansNo') IS NOT NULL
            ALTER TABLE [dbo].[AtikFirmaTurleri] DROP COLUMN [LisansNo];");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmaTurleri', N'LisansNo') IS NULL
            ALTER TABLE [dbo].[AtikFirmaTurleri] ADD [LisansNo] NVARCHAR(120) NULL;");
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmalari', N'LisansNo') IS NOT NULL
            EXEC(N'UPDATE t SET t.LisansNo = f.LisansNo
                FROM dbo.AtikFirmaTurleri AS t INNER JOIN dbo.AtikFirmalari AS f ON f.AtikFirmaTuruId = t.Id
                WHERE t.LisansNo IS NULL AND f.LisansNo IS NOT NULL');");
        migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.AtikFirmalari', N'LisansNo') IS NOT NULL
            ALTER TABLE [dbo].[AtikFirmalari] DROP COLUMN [LisansNo];");
    }
}
