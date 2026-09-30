using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace IsgCevreYonetim.Infrastructure.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927230000_AddAtikYonetimi")]
public class AddAtikYonetimi : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.AtikTurleri', N'U') IS NULL BEGIN CREATE TABLE [dbo].[AtikTurleri] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [BranchId] INT NOT NULL, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [AtikTurAdi] NVARCHAR(120) NOT NULL); CREATE UNIQUE INDEX [IX_AtikTurleri_BranchId_AtikTurAdi] ON [AtikTurleri]([BranchId],[AtikTurAdi]); END");
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.AtikFirmaTurleri', N'U') IS NULL BEGIN CREATE TABLE [dbo].[AtikFirmaTurleri] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [BranchId] INT NOT NULL, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [AtikFirmaTuruAdi] NVARCHAR(120) NOT NULL, [LisansNo] NVARCHAR(120) NULL); CREATE UNIQUE INDEX [IX_AtikFirmaTurleri_BranchId_AtikFirmaTuruAdi] ON [AtikFirmaTurleri]([BranchId],[AtikFirmaTuruAdi]); END");
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.AtikFirmalari', N'U') IS NULL BEGIN CREATE TABLE [dbo].[AtikFirmalari] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [BranchId] INT NOT NULL, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [AtikFirmaAdi] NVARCHAR(200) NOT NULL, [AtikFirmaTuruId] INT NOT NULL, CONSTRAINT [FK_AtikFirmalari_AtikFirmaTurleri_AtikFirmaTuruId] FOREIGN KEY ([AtikFirmaTuruId]) REFERENCES [AtikFirmaTurleri]([Id])); CREATE UNIQUE INDEX [IX_AtikFirmalari_BranchId_AtikFirmaAdi] ON [AtikFirmalari]([BranchId],[AtikFirmaAdi]); CREATE INDEX [IX_AtikFirmalari_AtikFirmaTuruId] ON [AtikFirmalari]([AtikFirmaTuruId]); END");
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.Atiklar', N'U') IS NULL BEGIN CREATE TABLE [dbo].[Atiklar] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [BranchId] INT NOT NULL, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [AtikAdi] NVARCHAR(200) NOT NULL, [AtikKodu] NVARCHAR(30) NOT NULL, [AtikTuruId] INT NOT NULL, CONSTRAINT [FK_Atiklar_AtikTurleri_AtikTuruId] FOREIGN KEY ([AtikTuruId]) REFERENCES [AtikTurleri]([Id])); CREATE UNIQUE INDEX [IX_Atiklar_BranchId_AtikKodu] ON [Atiklar]([BranchId],[AtikKodu]); CREATE INDEX [IX_Atiklar_AtikTuruId] ON [Atiklar]([AtikTuruId]); END");
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.AtikTakipleri', N'U') IS NULL BEGIN CREATE TABLE [dbo].[AtikTakipleri] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [BranchId] INT NOT NULL, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [TasimaMotAtNo] NVARCHAR(100) NOT NULL, [Tarih] DATETIME2 NOT NULL, [AtikId] INT NOT NULL, [Miktar] DECIMAL(18,3) NOT NULL, [OdemeSatis] BIT NOT NULL, [BertarafBedeli] DECIMAL(18,2) NOT NULL, [NakliyeBedeli] DECIMAL(18,2) NOT NULL, [IslemeYontemi] NVARCHAR(200) NOT NULL, [HesaplananTutar] DECIMAL(18,2) NOT NULL, [TasiyiciFirmaId] INT NOT NULL, [AliciFirmaId] INT NOT NULL, [AliciLisansNo] NVARCHAR(120) NULL, CONSTRAINT [FK_AtikTakipleri_Atiklar_AtikId] FOREIGN KEY ([AtikId]) REFERENCES [Atiklar]([Id]), CONSTRAINT [FK_AtikTakipleri_AtikFirmalari_TasiyiciFirmaId] FOREIGN KEY ([TasiyiciFirmaId]) REFERENCES [AtikFirmalari]([Id]), CONSTRAINT [FK_AtikTakipleri_AtikFirmalari_AliciFirmaId] FOREIGN KEY ([AliciFirmaId]) REFERENCES [AtikFirmalari]([Id])); CREATE INDEX [IX_AtikTakipleri_BranchId_Tarih] ON [AtikTakipleri]([BranchId],[Tarih]); CREATE INDEX [IX_AtikTakipleri_AtikId] ON [AtikTakipleri]([AtikId]); CREATE INDEX [IX_AtikTakipleri_TasiyiciFirmaId] ON [AtikTakipleri]([TasiyiciFirmaId]); CREATE INDEX [IX_AtikTakipleri_AliciFirmaId] ON [AtikTakipleri]([AliciFirmaId]); END");
  migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.AtikTakipDosyalari', N'U') IS NULL BEGIN CREATE TABLE [dbo].[AtikTakipDosyalari] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), [UpdatedDate] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1, [IsDeleted] BIT NOT NULL DEFAULT 0, [AtikTakibiId] INT NOT NULL, [DosyaAdi] NVARCHAR(255) NOT NULL, [IcerikTuru] NVARCHAR(150) NOT NULL, [SaklamaAdi] NVARCHAR(100) NOT NULL, [Boyut] BIGINT NOT NULL, CONSTRAINT [FK_AtikTakipDosyalari_AtikTakipleri_AtikTakibiId] FOREIGN KEY ([AtikTakibiId]) REFERENCES [AtikTakipleri]([Id]) ON DELETE CASCADE); CREATE INDEX [IX_AtikTakipDosyalari_AtikTakibiId] ON [AtikTakipDosyalari]([AtikTakibiId]); END");
  migrationBuilder.Sql(@"
DECLARE @pages TABLE ([Ad] NVARCHAR(100), [Url] NVARCHAR(200), [Icon] NVARCHAR(50), [Sira] INT);
INSERT INTO @pages VALUES
(N'Atık Türleri', N'/Cevre/Turler', N'fa-list', 31),
(N'Atık Firma Türleri', N'/Cevre/FirmaTurleri', N'fa-id-card', 32),
(N'Atık Firmaları', N'/Cevre/Firmalar', N'fa-truck', 33),
(N'Atıklar', N'/Cevre/Atiklar', N'fa-recycle', 34),
(N'Atık Takibi', N'/Cevre/Takip', N'fa-clipboard-list', 35);
INSERT INTO dbo.Sayfalar ([Ad],[Url],[Icon],[Sira],[CreatedDate],[IsActive],[IsDeleted])
SELECT p.Ad,p.Url,p.Icon,p.Sira,GETDATE(),1,0 FROM @pages p
WHERE NOT EXISTS(SELECT 1 FROM dbo.Sayfalar s WHERE s.Url=p.Url);
INSERT INTO dbo.YetkiSayfalar ([YetkiId],[SayfaId],[BranchId],[Goster],[Ekle],[Guncelle],[Sil],[CreatedDate],[IsActive],[IsDeleted])
SELECT y.YetkiId,p.Id,y.BranchId,y.Goster,y.Ekle,y.Guncelle,y.Sil,GETDATE(),1,0
FROM dbo.YetkiSayfalar y INNER JOIN dbo.Sayfalar source ON source.Id=y.SayfaId AND source.Url=N'/Cevre/Index'
CROSS JOIN dbo.Sayfalar p INNER JOIN @pages defs ON defs.Url=p.Url
WHERE y.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM dbo.YetkiSayfalar existing WHERE existing.YetkiId=y.YetkiId AND existing.BranchId=y.BranchId AND existing.SayfaId=p.Id);
");
 }
 protected override void Down(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.AtikTakipDosyalari', N'U') IS NOT NULL DROP TABLE [dbo].[AtikTakipDosyalari]");
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.AtikTakipleri', N'U') IS NOT NULL DROP TABLE [dbo].[AtikTakipleri]");
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.Atiklar', N'U') IS NOT NULL DROP TABLE [dbo].[Atiklar]");
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.AtikFirmalari', N'U') IS NOT NULL DROP TABLE [dbo].[AtikFirmalari]");
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.AtikFirmaTurleri', N'U') IS NOT NULL DROP TABLE [dbo].[AtikFirmaTurleri]");
  migrationBuilder.Sql("IF OBJECT_ID(N'dbo.AtikTurleri', N'U') IS NOT NULL DROP TABLE [dbo].[AtikTurleri]");
 }
}
