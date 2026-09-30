using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924001000_AddTehlikeliIsPerformanceIndexes")]
public partial class AddTehlikeliIsPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsler_Branch_Active_Date' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsler'))
    CREATE INDEX [IX_TehlikeliIsler_Branch_Active_Date] ON [dbo].[TehlikeliIsler] ([BranchId], [IsDeleted], [IsActive], [Tarih] DESC, [Saat] DESC) INCLUDE ([IsDurumuId], [CalismaYapacakBirimId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsGunleri_Is_GunNo' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsGunleri'))
    CREATE INDEX [IX_TehlikeliIsGunleri_Is_GunNo] ON [dbo].[TehlikeliIsGunleri] ([TehlikeliIsId], [GunNo]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsKisileri_Is_Gun' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsKisileri'))
    CREATE INDEX [IX_TehlikeliIsKisileri_Is_Gun] ON [dbo].[TehlikeliIsKisileri] ([TehlikeliIsId], [TehlikeliIsGunId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsGunSiniflari_Gun' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsGunTehlikeSiniflari'))
    CREATE INDEX [IX_TehlikeliIsGunSiniflari_Gun] ON [dbo].[TehlikeliIsGunTehlikeSiniflari] ([TehlikeliIsGunId], [TehlikeSinifiId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsGunMaddeleri_Gun' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsGunMaddeleri'))
    CREATE INDEX [IX_TehlikeliIsGunMaddeleri_Gun] ON [dbo].[TehlikeliIsGunMaddeleri] ([TehlikeliIsGunId], [MaddeId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsDosyalari_Is_Gun' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsDosyalari'))
    CREATE INDEX [IX_TehlikeliIsDosyalari_Is_Gun] ON [dbo].[TehlikeliIsDosyalari] ([TehlikeliIsId], [TehlikeliIsGunId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TehlikeliIsDenetim_Is_Id' AND object_id = OBJECT_ID(N'dbo.TehlikeliIsDenetimKayitlari'))
    CREATE INDEX [IX_TehlikeliIsDenetim_Is_Id] ON [dbo].[TehlikeliIsDenetimKayitlari] ([TehlikeliIsId], [Id] DESC);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP INDEX IF EXISTS [IX_TehlikeliIsDenetim_Is_Id] ON [dbo].[TehlikeliIsDenetimKayitlari];
DROP INDEX IF EXISTS [IX_TehlikeliIsDosyalari_Is_Gun] ON [dbo].[TehlikeliIsDosyalari];
DROP INDEX IF EXISTS [IX_TehlikeliIsGunMaddeleri_Gun] ON [dbo].[TehlikeliIsGunMaddeleri];
DROP INDEX IF EXISTS [IX_TehlikeliIsGunSiniflari_Gun] ON [dbo].[TehlikeliIsGunTehlikeSiniflari];
DROP INDEX IF EXISTS [IX_TehlikeliIsKisileri_Is_Gun] ON [dbo].[TehlikeliIsKisileri];
DROP INDEX IF EXISTS [IX_TehlikeliIsGunleri_Is_GunNo] ON [dbo].[TehlikeliIsGunleri];
DROP INDEX IF EXISTS [IX_TehlikeliIsler_Branch_Active_Date] ON [dbo].[TehlikeliIsler];
");
    }
}
