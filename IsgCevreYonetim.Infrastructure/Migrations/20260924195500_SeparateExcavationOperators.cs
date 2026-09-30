using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924195500_SeparateExcavationOperators")]
public partial class SeparateExcavationOperators : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF COL_LENGTH('dbo.TehlikeliIsGunleri', 'KaziIsMakinesi') IS NULL
            ALTER TABLE dbo.TehlikeliIsGunleri ADD KaziIsMakinesi nvarchar(100) NULL;
            IF COL_LENGTH('dbo.TehlikeliIsGunleri', 'KaziOperatorAdiSoyadi') IS NULL
            ALTER TABLE dbo.TehlikeliIsGunleri ADD KaziOperatorAdiSoyadi nvarchar(200) NULL;");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF COL_LENGTH('dbo.TehlikeliIsGunleri', 'KaziOperatorAdiSoyadi') IS NOT NULL
            ALTER TABLE dbo.TehlikeliIsGunleri DROP COLUMN KaziOperatorAdiSoyadi;
            IF COL_LENGTH('dbo.TehlikeliIsGunleri', 'KaziIsMakinesi') IS NOT NULL
            ALTER TABLE dbo.TehlikeliIsGunleri DROP COLUMN KaziIsMakinesi;");
    }
}
