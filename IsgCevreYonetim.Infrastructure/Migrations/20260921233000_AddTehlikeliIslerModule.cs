using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260921233000_AddTehlikeliIslerModule")]
public class AddTehlikeliIslerModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[TehlikeSiniflari]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeSiniflari](
 [Id] int IDENTITY PRIMARY KEY,[Ad] nvarchar(150) NOT NULL,[Aciklama] nvarchar(500) NULL,[Sira] int NOT NULL DEFAULT 0,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_TehlikeSiniflari_Ad') CREATE UNIQUE INDEX [UX_TehlikeSiniflari_Ad] ON [dbo].[TehlikeSiniflari]([Ad]);

IF OBJECT_ID(N'[dbo].[TehlikeSinifiMaddeleri]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeSinifiMaddeleri](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeSinifiId] int NOT NULL,[Metin] nvarchar(1200) NOT NULL,[Sira] int NOT NULL DEFAULT 0,[ExcelSatirNo] int NULL,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeSinifiMaddeleri_TehlikeSiniflari] FOREIGN KEY([TehlikeSinifiId]) REFERENCES [dbo].[TehlikeSiniflari]([Id]) ON DELETE CASCADE);
CREATE INDEX [IX_TehlikeSinifiMaddeleri_Sinif_Sira] ON [dbo].[TehlikeSinifiMaddeleri]([TehlikeSinifiId],[Sira]);

IF OBJECT_ID(N'[dbo].[IsDurumlari]', N'U') IS NULL CREATE TABLE [dbo].[IsDurumlari](
 [Id] int IDENTITY PRIMARY KEY,[Ad] nvarchar(100) NOT NULL,[Renk] nvarchar(30) NOT NULL DEFAULT '#64748b',[Sira] int NOT NULL DEFAULT 0,
 [DevamKaydiAcabilir] bit NOT NULL DEFAULT 0,[Tamamlandi] bit NOT NULL DEFAULT 0,[CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_IsDurumlari_Ad') CREATE UNIQUE INDEX [UX_IsDurumlari_Ad] ON [dbo].[IsDurumlari]([Ad]);

IF OBJECT_ID(N'[dbo].[TehlikeliIsler]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsler](
 [Id] int IDENTITY PRIMARY KEY,[Tarih] datetime2 NOT NULL,[Saat] time NOT NULL,[BranchId] int NOT NULL,[CalismaYapacakBirimId] int NOT NULL,
 [CalismaYapilacakYer] nvarchar(300) NOT NULL,[YapilacakIsAciklamasi] nvarchar(2000) NOT NULL,[CalisanPersonelSayisi] int NOT NULL,
 [TehlikeSinifiId] int NOT NULL,[IsDurumuId] int NOT NULL,[IsiYaptiranPersonelId] int NOT NULL,[FirmaSorumlusuPersonelId] int NOT NULL,
 [KontrolEdenPersonelId] int NOT NULL,[OnaylayanPersonelId] int NOT NULL,[Aciklama] nvarchar(4000) NULL,[DogrulamaKodu] nvarchar(64) NOT NULL,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeliIsler_Branches] FOREIGN KEY([BranchId]) REFERENCES [dbo].[Branches]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_Departments] FOREIGN KEY([CalismaYapacakBirimId]) REFERENCES [dbo].[Departments]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_TehlikeSiniflari] FOREIGN KEY([TehlikeSinifiId]) REFERENCES [dbo].[TehlikeSiniflari]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_IsDurumlari] FOREIGN KEY([IsDurumuId]) REFERENCES [dbo].[IsDurumlari]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_IsiYaptiran] FOREIGN KEY([IsiYaptiranPersonelId]) REFERENCES [dbo].[Personeller]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_FirmaSorumlusu] FOREIGN KEY([FirmaSorumlusuPersonelId]) REFERENCES [dbo].[Personeller]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_KontrolEden] FOREIGN KEY([KontrolEdenPersonelId]) REFERENCES [dbo].[Personeller]([Id]),
 CONSTRAINT [FK_TehlikeliIsler_Onaylayan] FOREIGN KEY([OnaylayanPersonelId]) REFERENCES [dbo].[Personeller]([Id]));
CREATE UNIQUE INDEX [UX_TehlikeliIsler_DogrulamaKodu] ON [dbo].[TehlikeliIsler]([DogrulamaKodu]);
CREATE INDEX [IX_TehlikeliIsler_Branch_Tarih] ON [dbo].[TehlikeliIsler]([BranchId],[IsDeleted],[Tarih]);

IF OBJECT_ID(N'[dbo].[TehlikeliIsGunleri]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsGunleri](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeliIsId] int NOT NULL,[GunNo] int NOT NULL,[Tarih] datetime2 NOT NULL,[KontrolEdenPersonelId] int NOT NULL,[OnaylayanPersonelId] int NOT NULL,
 [Aciklama] nvarchar(2000) NULL,[KontrolSistemOnayTarihi] datetime2 NULL,[OnaySistemOnayTarihi] datetime2 NULL,[KontrolOnayOzeti] nvarchar(64) NULL,[OnayOzeti] nvarchar(64) NULL,
 [IslakImzaliBelgeYolu] nvarchar(500) NULL,[IslakImzaliBelgeSha256] nvarchar(64) NULL,[CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [CK_TehlikeliIsGunleri_GunNo] CHECK([GunNo] BETWEEN 1 AND 7),
 CONSTRAINT [FK_TehlikeliIsGunleri_Is] FOREIGN KEY([TehlikeliIsId]) REFERENCES [dbo].[TehlikeliIsler]([Id]) ON DELETE CASCADE,
 CONSTRAINT [FK_TehlikeliIsGunleri_Kontrol] FOREIGN KEY([KontrolEdenPersonelId]) REFERENCES [dbo].[Personeller]([Id]),
 CONSTRAINT [FK_TehlikeliIsGunleri_Onay] FOREIGN KEY([OnaylayanPersonelId]) REFERENCES [dbo].[Personeller]([Id]));
CREATE UNIQUE INDEX [UX_TehlikeliIsGunleri_Is_Gun] ON [dbo].[TehlikeliIsGunleri]([TehlikeliIsId],[GunNo]);

IF OBJECT_ID(N'[dbo].[TehlikeliIsGunMaddeleri]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsGunMaddeleri](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeliIsGunId] int NOT NULL,[MaddeId] int NOT NULL,[Uygun] bit NOT NULL DEFAULT 1,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeliIsGunMaddeleri_Gun] FOREIGN KEY([TehlikeliIsGunId]) REFERENCES [dbo].[TehlikeliIsGunleri]([Id]) ON DELETE CASCADE,
 CONSTRAINT [FK_TehlikeliIsGunMaddeleri_Madde] FOREIGN KEY([MaddeId]) REFERENCES [dbo].[TehlikeSinifiMaddeleri]([Id]));
CREATE UNIQUE INDEX [UX_TehlikeliIsGunMaddeleri] ON [dbo].[TehlikeliIsGunMaddeleri]([TehlikeliIsGunId],[MaddeId]);

IF OBJECT_ID(N'[dbo].[TehlikeliIsKisileri]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsKisileri](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeliIsId] int NOT NULL,[TehlikeliIsGunId] int NULL,[PersonelId] int NULL,[AdSoyad] nvarchar(200) NOT NULL,[Firma] nvarchar(200) NULL,[Rol] nvarchar(50) NOT NULL,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeliIsKisileri_Is] FOREIGN KEY([TehlikeliIsId]) REFERENCES [dbo].[TehlikeliIsler]([Id]) ON DELETE CASCADE,
 CONSTRAINT [FK_TehlikeliIsKisileri_Gun] FOREIGN KEY([TehlikeliIsGunId]) REFERENCES [dbo].[TehlikeliIsGunleri]([Id]),
 CONSTRAINT [FK_TehlikeliIsKisileri_Personel] FOREIGN KEY([PersonelId]) REFERENCES [dbo].[Personeller]([Id]));

IF OBJECT_ID(N'[dbo].[TehlikeliIsDosyalari]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsDosyalari](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeliIsId] int NOT NULL,[TehlikeliIsGunId] int NULL,[DosyaAdi] nvarchar(260) NOT NULL,[DosyaYolu] nvarchar(500) NOT NULL,[DosyaTipi] nvarchar(50) NOT NULL,[Sha256] nvarchar(64) NOT NULL,[DosyaBoyutu] bigint NOT NULL,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeliIsDosyalari_Is] FOREIGN KEY([TehlikeliIsId]) REFERENCES [dbo].[TehlikeliIsler]([Id]) ON DELETE CASCADE,
 CONSTRAINT [FK_TehlikeliIsDosyalari_Gun] FOREIGN KEY([TehlikeliIsGunId]) REFERENCES [dbo].[TehlikeliIsGunleri]([Id]));

IF OBJECT_ID(N'[dbo].[TehlikeliIsDenetimKayitlari]', N'U') IS NULL CREATE TABLE [dbo].[TehlikeliIsDenetimKayitlari](
 [Id] int IDENTITY PRIMARY KEY,[TehlikeliIsId] int NOT NULL,[TehlikeliIsGunId] int NULL,[PersonelId] int NULL,[Islem] nvarchar(100) NOT NULL,[Aciklama] nvarchar(1000) NOT NULL,
 [IpAdresi] nvarchar(50) NULL,[UserAgent] nvarchar(500) NULL,[OncekiKayitOzeti] nvarchar(64) NOT NULL,[KayitOzeti] nvarchar(64) NOT NULL,
 [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE(),[UpdatedDate] datetime2 NULL,[IsActive] bit NOT NULL DEFAULT 1,[IsDeleted] bit NOT NULL DEFAULT 0,
 CONSTRAINT [FK_TehlikeliIsDenetim_Is] FOREIGN KEY([TehlikeliIsId]) REFERENCES [dbo].[TehlikeliIsler]([Id]) ON DELETE CASCADE,
 CONSTRAINT [FK_TehlikeliIsDenetim_Gun] FOREIGN KEY([TehlikeliIsGunId]) REFERENCES [dbo].[TehlikeliIsGunleri]([Id]),
 CONSTRAINT [FK_TehlikeliIsDenetim_Personel] FOREIGN KEY([PersonelId]) REFERENCES [dbo].[Personeller]([Id]));

IF OBJECT_ID(N'[dbo].[TR_TehlikeliIsDenetim_Immutable]', N'TR') IS NULL EXEC(N'CREATE TRIGGER [dbo].[TR_TehlikeliIsDenetim_Immutable] ON [dbo].[TehlikeliIsDenetimKayitlari] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF @@ROWCOUNT > 0 BEGIN ROLLBACK TRANSACTION; THROW 51000, ''Denetim kayıtları değiştirilemez veya silinemez.'', 1; END END');

IF NOT EXISTS(SELECT 1 FROM [dbo].[IsDurumlari] WHERE [Ad]=N'Yeni') INSERT INTO [dbo].[IsDurumlari]([Ad],[Renk],[Sira],[DevamKaydiAcabilir],[Tamamlandi]) VALUES(N'Yeni',N'#2563eb',1,0,0);
IF NOT EXISTS(SELECT 1 FROM [dbo].[IsDurumlari] WHERE [Ad]=N'Devam Eden') INSERT INTO [dbo].[IsDurumlari]([Ad],[Renk],[Sira],[DevamKaydiAcabilir],[Tamamlandi]) VALUES(N'Devam Eden',N'#f59e0b',2,1,0);
IF NOT EXISTS(SELECT 1 FROM [dbo].[IsDurumlari] WHERE [Ad]=N'Tamamlandı') INSERT INTO [dbo].[IsDurumlari]([Ad],[Renk],[Sira],[DevamKaydiAcabilir],[Tamamlandi]) VALUES(N'Tamamlandı',N'#059669',3,0,1);

DECLARE @A int,@Y int,@K int,@C int;
IF NOT EXISTS(SELECT 1 FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Ateşli (Kıvılcım Çıkaran) İşler') INSERT INTO [dbo].[TehlikeSiniflari]([Ad],[Sira]) VALUES(N'Ateşli (Kıvılcım Çıkaran) İşler',1);
IF NOT EXISTS(SELECT 1 FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Yüksekte Çalışma') INSERT INTO [dbo].[TehlikeSiniflari]([Ad],[Sira]) VALUES(N'Yüksekte Çalışma',2);
IF NOT EXISTS(SELECT 1 FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Kazı Çalışması') INSERT INTO [dbo].[TehlikeSiniflari]([Ad],[Sira]) VALUES(N'Kazı Çalışması',3);
IF NOT EXISTS(SELECT 1 FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Kapalı Alan / Kimyasal / Basınçlı Hat') INSERT INTO [dbo].[TehlikeSiniflari]([Ad],[Sira]) VALUES(N'Kapalı Alan / Kimyasal / Basınçlı Hat',4);
SELECT @A=Id FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Ateşli (Kıvılcım Çıkaran) İşler'; SELECT @Y=Id FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Yüksekte Çalışma'; SELECT @K=Id FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Kazı Çalışması'; SELECT @C=Id FROM [dbo].[TehlikeSiniflari] WHERE [Ad]=N'Kapalı Alan / Kimyasal / Basınçlı Hat';
IF NOT EXISTS(SELECT 1 FROM [dbo].[TehlikeSinifiMaddeleri]) BEGIN
INSERT INTO [dbo].[TehlikeSinifiMaddeleri]([TehlikeSinifiId],[Metin],[Sira],[ExcelSatirNo]) VALUES
(@A,N'Kaynak tüpü, şaloma, hortum ve alev tutucular uygun mu?',1,25),(@A,N'El aletleri ve elektrik kabloları güvenli mi?',2,26),(@A,N'EKED uygulaması ve hat temizliği yapıldı mı?',3,27),(@A,N'Yanıcı ve patlayıcı maddeler uzaklaştırıldı mı?',4,28),(@A,N'Ortam ölçümü yapıldı mı?',5,29),(@A,N'Ekipman basıncı düşürüldü ve temizlendi mi?',6,30),(@A,N'Tehlikeli bölgede sürekli nezaret var mı?',7,31),(@A,N'Pnömatik hat güvenli hale getirildi mi?',8,32),(@A,N'Yanıcı ekipmanlar yanmaz battaniye ile korundu mu?',9,33),(@A,N'Kaynak perdesi kullanılıyor mu?',10,34),(@A,N'Uygun yangın söndürücü mevcut mu?',11,35),(@A,N'Kaynak KKD ekipmanları uygun mu?',12,36),(@A,N'Kaynak şasesi yeterince yakın mı?',13,37),(@A,N'Diğer ateşli iş önlemleri uygun mu?',14,38),
(@Y,N'Personelin sağlık durumu yüksekte çalışmaya uygun mu?',1,40),(@Y,N'Hava şartları uygun mu?',2,41),(@Y,N'Operatör ehliyeti ve ekipman sabitlemesi uygun mu?',3,42),(@Y,N'Paraşüt tipi emniyet kemeri kullanılıyor mu?',4,43),(@Y,N'Güvenli bağlantı noktası oluşturuldu mu?',5,44),(@Y,N'Yük ve kaldırma yöntemi uygun mu?',6,45),(@Y,N'Periyodik kontroller yapılmış mı?',7,46),(@Y,N'Düşebilecek malzemeler ve saha emniyete alındı mı?',8,47),(@Y,N'Elektrik hatlarına güvenli mesafe var mı?',9,48),(@Y,N'Operatör bilgisi kaydedildi mi?',10,49),
(@K,N'Yeraltı ve yerüstü hatları kontrol edildi mi?',1,51),(@K,N'İş alanı çevrildi ve işaretlendi mi?',2,52),(@K,N'İksa ve destekler uygun mu?',3,53),(@K,N'Kazı toprağı güvenli mesafeye taşındı mı?',4,54),(@K,N'Operatör bilgisi kaydedildi mi?',5,55),
(@C,N'Hareketli ve basınçlı ekipmanlar güvenli hale getirildi mi?',1,57),(@C,N'Ekipman boşaltıldı, temizlendi ve izole edildi mi?',2,58),(@C,N'EKED işlemleri yapıldı mı?',3,59),(@C,N'Gözcü personel görevlendirildi mi?',4,60),(@C,N'Alan temizlendi ve havalandırıldı mı?',5,61),(@C,N'Ortam gaz ölçümü yapıldı mı?',6,62),(@C,N'Gaz ölçüm sonuçları kaydedildi mi?',7,63),(@C,N'Saha sorumluları ve operatörler bilgilendirildi mi?',8,64);
END

IF NOT EXISTS(SELECT 1 FROM [dbo].[Sayfalar] WHERE [Url]=N'/TehlikeliIs/Index') INSERT INTO [dbo].[Sayfalar]([Ad],[Url],[Icon],[ParentId],[Sira],[CreatedDate],[IsActive],[IsDeleted]) VALUES(N'Tehlikeli İşler',N'/TehlikeliIs/Index',N'fa-person-digging',NULL,25,GETDATE(),1,0);
IF NOT EXISTS(SELECT 1 FROM [dbo].[Sayfalar] WHERE [Url]=N'/TehlikeSinifi/Index') INSERT INTO [dbo].[Sayfalar]([Ad],[Url],[Icon],[ParentId],[Sira],[CreatedDate],[IsActive],[IsDeleted]) VALUES(N'Tehlike Sınıfları',N'/TehlikeSinifi/Index',N'fa-triangle-exclamation',NULL,26,GETDATE(),1,0);
IF NOT EXISTS(SELECT 1 FROM [dbo].[Sayfalar] WHERE [Url]=N'/IsDurumu/Index') INSERT INTO [dbo].[Sayfalar]([Ad],[Url],[Icon],[ParentId],[Sira],[CreatedDate],[IsActive],[IsDeleted]) VALUES(N'İş Durumları',N'/IsDurumu/Index',N'fa-list-check',NULL,27,GETDATE(),1,0);
");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[TR_TehlikeliIsDenetim_Immutable]', N'TR') IS NOT NULL DROP TRIGGER [dbo].[TR_TehlikeliIsDenetim_Immutable];
DROP TABLE IF EXISTS [dbo].[TehlikeliIsDenetimKayitlari]; DROP TABLE IF EXISTS [dbo].[TehlikeliIsDosyalari]; DROP TABLE IF EXISTS [dbo].[TehlikeliIsKisileri]; DROP TABLE IF EXISTS [dbo].[TehlikeliIsGunMaddeleri]; DROP TABLE IF EXISTS [dbo].[TehlikeliIsGunleri]; DROP TABLE IF EXISTS [dbo].[TehlikeliIsler]; DROP TABLE IF EXISTS [dbo].[TehlikeSinifiMaddeleri]; DROP TABLE IF EXISTS [dbo].[TehlikeSiniflari]; DROP TABLE IF EXISTS [dbo].[IsDurumlari];");
}
