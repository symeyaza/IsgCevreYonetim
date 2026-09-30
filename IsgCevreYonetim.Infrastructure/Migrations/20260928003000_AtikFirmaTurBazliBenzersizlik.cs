using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928003000_AtikFirmaTurBazliBenzersizlik")]
public class AtikFirmaTurBazliBenzersizlik : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AtikFirmalari') AND name = N'IX_AtikFirmalari_BranchId_AtikFirmaAdi')
            DROP INDEX [IX_AtikFirmalari_BranchId_AtikFirmaAdi] ON [dbo].[AtikFirmalari];");
        migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AtikFirmalari') AND name = N'IX_AtikFirmalari_BranchId_AtikFirmaTuruId_AtikFirmaAdi')
            CREATE UNIQUE INDEX [IX_AtikFirmalari_BranchId_AtikFirmaTuruId_AtikFirmaAdi] ON [dbo].[AtikFirmalari] ([BranchId], [AtikFirmaTuruId], [AtikFirmaAdi]);");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AtikFirmalari') AND name = N'IX_AtikFirmalari_BranchId_AtikFirmaTuruId_AtikFirmaAdi')
            DROP INDEX [IX_AtikFirmalari_BranchId_AtikFirmaTuruId_AtikFirmaAdi] ON [dbo].[AtikFirmalari];");
        migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AtikFirmalari') AND name = N'IX_AtikFirmalari_BranchId_AtikFirmaAdi')
            CREATE UNIQUE INDEX [IX_AtikFirmalari_BranchId_AtikFirmaAdi] ON [dbo].[AtikFirmalari] ([BranchId], [AtikFirmaAdi]);");
    }
}
