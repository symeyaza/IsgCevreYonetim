using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923223000_AddBildirimler")]
public class AddBildirimler : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        IF OBJECT_ID(N'[dbo].[Bildirimler]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[Bildirimler] (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Bildirimler] PRIMARY KEY,
                [PersonelId] int NOT NULL,
                [Baslik] nvarchar(180) NOT NULL,
                [Mesaj] nvarchar(600) NOT NULL,
                [Url] nvarchar(500) NOT NULL,
                [Tur] nvarchar(80) NOT NULL,
                [ReferansAnahtari] nvarchar(180) NOT NULL,
                [OkunduMu] bit NOT NULL CONSTRAINT [DF_Bildirimler_OkunduMu] DEFAULT(0),
                [OkunmaTarihi] datetime2 NULL,
                [CreatedDate] datetime2 NOT NULL,
                [UpdatedDate] datetime2 NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_Bildirimler_IsActive] DEFAULT(1),
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_Bildirimler_IsDeleted] DEFAULT(0),
                CONSTRAINT [FK_Bildirimler_Personeller_PersonelId] FOREIGN KEY ([PersonelId]) REFERENCES [dbo].[Personeller]([Id]) ON DELETE CASCADE
            );
        END;
        """);

        migrationBuilder.Sql("""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Bildirimler_ReferansAnahtari' AND object_id=OBJECT_ID(N'[dbo].[Bildirimler]'))
            CREATE UNIQUE INDEX [IX_Bildirimler_ReferansAnahtari] ON [dbo].[Bildirimler]([ReferansAnahtari]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Bildirimler_PersonelId_OkunduMu_CreatedDate' AND object_id=OBJECT_ID(N'[dbo].[Bildirimler]'))
            CREATE INDEX [IX_Bildirimler_PersonelId_OkunduMu_CreatedDate] ON [dbo].[Bildirimler]([PersonelId],[OkunduMu],[CreatedDate] DESC);
        """);

        migrationBuilder.Sql("""
        INSERT INTO [dbo].[Bildirimler] ([PersonelId],[Baslik],[Mesaj],[Url],[Tur],[ReferansAnahtari],[OkunduMu],[CreatedDate],[IsActive],[IsDeleted])
        SELECT i.[IsiYaptiranPersonelId],N'Tehlikeli iş onayı bekliyor',CONCAT(N'#',i.[Id],N' numaralı iş için İşi Yaptıran / İşi Veren onayınız bekleniyor.'),
               CONCAT(N'/TehlikeliIs/Detay/',i.[Id],N'#ana-onaylar'),N'tehlikeli-is',CONCAT(N'tehlikeli-is:',i.[Id],N':yaptiran'),0,GETDATE(),1,0
        FROM [dbo].[TehlikeliIsler] i
        WHERE i.[IsDeleted]=0 AND i.[IsiYaptiranSistemOnayTarihi] IS NULL
          AND NOT EXISTS(SELECT 1 FROM [dbo].[Bildirimler] b WHERE b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',i.[Id],N':yaptiran'));

        INSERT INTO [dbo].[Bildirimler] ([PersonelId],[Baslik],[Mesaj],[Url],[Tur],[ReferansAnahtari],[OkunduMu],[CreatedDate],[IsActive],[IsDeleted])
        SELECT i.[FirmaSorumlusuPersonelId],N'Tehlikeli iş onayı bekliyor',CONCAT(N'#',i.[Id],N' numaralı iş için Firma Sorumlusu / Amiri onayınız bekleniyor.'),
               CONCAT(N'/TehlikeliIs/Detay/',i.[Id],N'#ana-onaylar'),N'tehlikeli-is',CONCAT(N'tehlikeli-is:',i.[Id],N':firma'),0,GETDATE(),1,0
        FROM [dbo].[TehlikeliIsler] i
        WHERE i.[IsDeleted]=0 AND i.[FirmaSorumlusuSistemOnayTarihi] IS NULL
          AND NOT EXISTS(SELECT 1 FROM [dbo].[Bildirimler] b WHERE b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',i.[Id],N':firma'));

        INSERT INTO [dbo].[Bildirimler] ([PersonelId],[Baslik],[Mesaj],[Url],[Tur],[ReferansAnahtari],[OkunduMu],[CreatedDate],[IsActive],[IsDeleted])
        SELECT g.[KontrolEdenPersonelId],N'Günlük kontrol onayı bekliyor',CONCAT(N'#',g.[TehlikeliIsId],N' numaralı tehlikeli işin ',g.[GunNo],N'. günü için Kontrol Eden onayınız bekleniyor.'),
               CONCAT(N'/TehlikeliIs/Detay/',g.[TehlikeliIsId],N'#gun-',g.[GunNo]),N'tehlikeli-is-gun',CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':kontrol'),0,GETDATE(),1,0
        FROM [dbo].[TehlikeliIsGunleri] g
        WHERE g.[IsDeleted]=0 AND g.[KontrolSistemOnayTarihi] IS NULL
          AND NOT EXISTS(SELECT 1 FROM [dbo].[Bildirimler] b WHERE b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':kontrol'));

        INSERT INTO [dbo].[Bildirimler] ([PersonelId],[Baslik],[Mesaj],[Url],[Tur],[ReferansAnahtari],[OkunduMu],[CreatedDate],[IsActive],[IsDeleted])
        SELECT g.[OnaylayanPersonelId],N'Günlük onay bekliyor',CONCAT(N'#',g.[TehlikeliIsId],N' numaralı tehlikeli işin ',g.[GunNo],N'. günü için Onaylayan onayınız bekleniyor.'),
               CONCAT(N'/TehlikeliIs/Detay/',g.[TehlikeliIsId],N'#gun-',g.[GunNo]),N'tehlikeli-is-gun',CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':onay'),0,GETDATE(),1,0
        FROM [dbo].[TehlikeliIsGunleri] g
        WHERE g.[IsDeleted]=0 AND g.[OnaySistemOnayTarihi] IS NULL
          AND NOT EXISTS(SELECT 1 FROM [dbo].[Bildirimler] b WHERE b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':onay'));
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF OBJECT_ID(N'[dbo].[Bildirimler]', N'U') IS NOT NULL DROP TABLE [dbo].[Bildirimler];
        """);
}
