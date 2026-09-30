using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260919234500_AddDashboardGuncellemeleri")]
    public partial class AddDashboardGuncellemeleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Canlı veritabanında güvenle tekrar çalıştırılabilir.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[DashboardGuncellemeleri]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DashboardGuncellemeleri] (
        [Id] int NOT NULL IDENTITY(1,1),
        [SayfaAdi] nvarchar(150) NOT NULL,
        [Kategori] int NOT NULL,
        [Durum] int NOT NULL,
        [Aciklama] nvarchar(500) NULL,
        [Sira] int NOT NULL CONSTRAINT [DF_DashboardGuncellemeleri_Sira] DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_DashboardGuncellemeleri] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DashboardGuncellemeleri_Kategori_Durum_Sira_IsDeleted'
      AND object_id = OBJECT_ID(N'[dbo].[DashboardGuncellemeleri]'))
BEGIN
    CREATE INDEX [IX_DashboardGuncellemeleri_Kategori_Durum_Sira_IsDeleted]
        ON [dbo].[DashboardGuncellemeleri] ([Kategori], [Durum], [Sira], [IsDeleted]);
END;

-- Eski statik Dashboard içeriğini yalnız tablo boşsa başlangıç verisi olarak aktar.
IF NOT EXISTS (SELECT 1 FROM [dbo].[DashboardGuncellemeleri])
BEGIN
    INSERT INTO [dbo].[DashboardGuncellemeleri]
        ([SayfaAdi],[Kategori],[Durum],[Aciklama],[Sira],[CreatedDate],[UpdatedDate],[IsActive],[IsDeleted])
    VALUES
        (N'Şirketler',1,1,NULL,10,GETDATE(),NULL,1,0),
        (N'Şubeler',1,1,NULL,20,GETDATE(),NULL,1,0),
        (N'Departmanlar',1,1,NULL,30,GETDATE(),NULL,1,0),
        (N'Birimler',1,1,NULL,40,GETDATE(),NULL,1,0),
        (N'İller',1,1,NULL,50,GETDATE(),NULL,1,0),
        (N'İlçeler',1,1,NULL,60,GETDATE(),NULL,1,0),
        (N'Cinsiyet',1,1,NULL,70,GETDATE(),NULL,1,0),
        (N'Görevler',1,1,NULL,80,GETDATE(),NULL,1,0),
        (N'Gruplar',1,1,NULL,90,GETDATE(),NULL,1,0),
        (N'Vardiyalar',1,1,NULL,100,GETDATE(),NULL,1,0),
        (N'Personel',1,1,NULL,110,GETDATE(),NULL,1,0),
        (N'Yetkiler ve Yetkilendirme',1,1,NULL,120,GETDATE(),NULL,1,0),
        (N'Dashboard',1,2,N'Özet kartlar ve modül göstergelerinin geliştirilmesi',10,GETDATE(),NULL,1,0),
        (N'Kullanıcı Deneyimi',1,2,N'Tema, responsive görünüm ve performans iyileştirmeleri',20,GETDATE(),NULL,1,0),
        (N'Bildirim Merkezi',1,3,N'Görev ve süreç bildirimleri',10,GETDATE(),NULL,1,0),
        (N'Doküman Yönetimi',1,3,N'Merkezi doküman ve revizyon takibi',20,GETDATE(),NULL,1,0),

        (N'İş Kazaları',2,1,NULL,10,GETDATE(),NULL,1,0),
        (N'İş Kazası Kategorileri',2,1,NULL,20,GETDATE(),NULL,1,0),
        (N'İş Kazası Maddeleri',2,1,NULL,30,GETDATE(),NULL,1,0),
        (N'İş Kazası Araştırma',2,1,NULL,40,GETDATE(),NULL,1,0),
        (N'İş Kazası Düzeltici Faaliyetler',2,1,NULL,50,GETDATE(),NULL,1,0),
        (N'İş Kazası Raporlama',2,1,NULL,60,GETDATE(),NULL,1,0),
        (N'Kaza Araştırma Excel Çıktısı',2,1,NULL,70,GETDATE(),NULL,1,0),
        (N'Tehlikeli İşler',2,2,N'Ateşli işler, yüksekte çalışma, kazı ve kapalı alan süreçleri',10,GETDATE(),NULL,1,0),
        (N'İş Bildirimleri',2,2,N'Sorumlu kullanıcı ve durum akışları',20,GETDATE(),NULL,1,0),
        (N'Risk Değerlendirme',2,3,NULL,10,GETDATE(),NULL,1,0),
        (N'Eğitim Takibi',2,3,NULL,20,GETDATE(),NULL,1,0),
        (N'Periyodik Kontroller',2,3,NULL,30,GETDATE(),NULL,1,0),
        (N'Ramak Kala',2,3,NULL,40,GETDATE(),NULL,1,0),

        (N'Çevre Ana Sayfası',3,1,N'Çevre modülü için temel sayfa ve yetkilendirme altyapısı',10,GETDATE(),NULL,1,0),
        (N'Çevre Dashboard Göstergeleri',3,2,N'Şube bazlı çevre KPI ve özet kartları',10,GETDATE(),NULL,1,0),
        (N'Çevre Kayıt Altyapısı',3,2,N'Modül ekranlarının hazırlanması',20,GETDATE(),NULL,1,0),
        (N'Atık Yönetimi',3,3,NULL,10,GETDATE(),NULL,1,0),
        (N'Su ve Atıksu Takibi',3,3,NULL,20,GETDATE(),NULL,1,0),
        (N'Emisyon Takibi',3,3,NULL,30,GETDATE(),NULL,1,0),
        (N'Çevre İzin ve Lisansları',3,3,NULL,40,GETDATE(),NULL,1,0),
        (N'Çevre Ölçüm ve Analizleri',3,3,NULL,50,GETDATE(),NULL,1,0);
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[DashboardGuncellemeleri]', N'U') IS NOT NULL
    DROP TABLE [dbo].[DashboardGuncellemeleri];
");
        }
    }
}
