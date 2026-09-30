using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928233000_AddTaseronFirmalarVeTehlikeliIs")]
public class AddTaseronFirmalarVeTehlikeliIs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.TaseronFirmalar', N'U') IS NULL BEGIN
 CREATE TABLE dbo.TaseronFirmalar (
  Id int IDENTITY PRIMARY KEY, CompanyId int NOT NULL, BranchId int NOT NULL,
  FirmaAdi nvarchar(200) NOT NULL, VergiNo nvarchar(100) NULL, Telefon nvarchar(100) NULL,
  CreatedDate datetime2 NOT NULL DEFAULT GETDATE(), UpdatedDate datetime2 NULL,
  IsActive bit NOT NULL DEFAULT 1, IsDeleted bit NOT NULL DEFAULT 0,
  CONSTRAINT FK_TaseronFirmalar_Companies FOREIGN KEY(CompanyId) REFERENCES dbo.Companies(Id),
  CONSTRAINT FK_TaseronFirmalar_Branches FOREIGN KEY(BranchId) REFERENCES dbo.Branches(Id));
 CREATE UNIQUE INDEX IX_TaseronFirmalar_CompanyId_BranchId_FirmaAdi ON dbo.TaseronFirmalar(CompanyId,BranchId,FirmaAdi);
END;
IF OBJECT_ID(N'dbo.TaseronKisiler', N'U') IS NULL BEGIN
 CREATE TABLE dbo.TaseronKisiler (
  Id int IDENTITY PRIMARY KEY, CompanyId int NOT NULL, BranchId int NOT NULL,
  TaseronFirmaId int NOT NULL, AdSoyad nvarchar(200) NOT NULL,
  KimlikNo nvarchar(50) NULL, Telefon nvarchar(100) NULL,
  YetkiliMi bit NOT NULL DEFAULT 0, CalisanMi bit NOT NULL DEFAULT 1,
  CreatedDate datetime2 NOT NULL DEFAULT GETDATE(), UpdatedDate datetime2 NULL,
  IsActive bit NOT NULL DEFAULT 1, IsDeleted bit NOT NULL DEFAULT 0,
  CONSTRAINT FK_TaseronKisiler_Firmalar FOREIGN KEY(TaseronFirmaId) REFERENCES dbo.TaseronFirmalar(Id));
 CREATE INDEX IX_TaseronKisiler_BranchId_TaseronFirmaId ON dbo.TaseronKisiler(BranchId,TaseronFirmaId);
END;
IF COL_LENGTH(N'dbo.TehlikeliIsler',N'TaseronFirmaId') IS NULL
 ALTER TABLE dbo.TehlikeliIsler ADD TaseronFirmaId int NULL, TaseronYetkiliId int NULL;
IF COL_LENGTH(N'dbo.TehlikeliIsKisileri',N'TaseronKisiId') IS NULL
 ALTER TABLE dbo.TehlikeliIsKisileri ADD TaseronKisiId int NULL;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TehlikeliIsler') AND name=N'IX_TehlikeliIsler_Branch_Active_Date')
 DROP INDEX IX_TehlikeliIsler_Branch_Active_Date ON dbo.TehlikeliIsler;
ALTER TABLE dbo.TehlikeliIsler ALTER COLUMN CalismaYapacakBirimId int NULL;
ALTER TABLE dbo.TehlikeliIsler ALTER COLUMN FirmaSorumlusuPersonelId int NULL;
CREATE INDEX IX_TehlikeliIsler_Branch_Active_Date ON dbo.TehlikeliIsler (BranchId,IsDeleted,IsActive,Tarih DESC,Saat DESC) INCLUDE (IsDurumuId,CalismaYapacakBirimId);
ALTER TABLE dbo.TehlikeliIsler ADD CONSTRAINT FK_TehlikeliIsler_TaseronFirma FOREIGN KEY(TaseronFirmaId) REFERENCES dbo.TaseronFirmalar(Id);
ALTER TABLE dbo.TehlikeliIsler ADD CONSTRAINT FK_TehlikeliIsler_TaseronYetkili FOREIGN KEY(TaseronYetkiliId) REFERENCES dbo.TaseronKisiler(Id);
ALTER TABLE dbo.TehlikeliIsKisileri ADD CONSTRAINT FK_TehlikeliIsKisileri_TaseronKisi FOREIGN KEY(TaseronKisiId) REFERENCES dbo.TaseronKisiler(Id);
CREATE INDEX IX_TehlikeliIsler_TaseronFirmaId ON dbo.TehlikeliIsler(TaseronFirmaId);
CREATE INDEX IX_TehlikeliIsKisileri_TaseronKisiId ON dbo.TehlikeliIsKisileri(TaseronKisiId);
IF NOT EXISTS(SELECT 1 FROM dbo.Sayfalar WHERE Url=N'/Taseron/Index')
 INSERT INTO dbo.Sayfalar(Ad,Url,Icon,Sira,CreatedDate,IsActive,IsDeleted)
 VALUES(N'Taşeron Firmalar',N'/Taseron/Index',N'fa-helmet-safety',36,GETDATE(),1,0);
INSERT INTO dbo.YetkiSayfalar(YetkiId,SayfaId,BranchId,Goster,Ekle,Guncelle,Sil,CreatedDate,IsActive,IsDeleted)
SELECT source.YetkiId, target.Id, source.BranchId, source.Goster, source.Ekle, source.Guncelle, source.Sil, GETDATE(),1,0
FROM dbo.YetkiSayfalar source
JOIN dbo.Sayfalar original ON original.Id=source.SayfaId AND original.Url=N'/TehlikeliIs/Index'
CROSS JOIN dbo.Sayfalar target
WHERE target.Url=N'/Taseron/Index' AND source.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM dbo.YetkiSayfalar previous WHERE previous.YetkiId=source.YetkiId AND previous.BranchId=source.BranchId AND previous.SayfaId=target.Id);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Taşeron kayıtları ve bunlara bağlı tehlikeli işler veri kaybı olmadan otomatik geri alınamaz.");
    }
}
