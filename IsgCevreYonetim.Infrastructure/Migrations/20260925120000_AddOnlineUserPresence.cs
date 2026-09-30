using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260925120000_AddOnlineUserPresence")]
    public partial class AddOnlineUserPresence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OnlineKullaniciOturumlari]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OnlineKullaniciOturumlari] (
        [SessionKey] nvarchar(64) NOT NULL,
        [PersonelId] int NOT NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [LastSeenUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OnlineKullaniciOturumlari] PRIMARY KEY ([SessionKey])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnlineKullaniciOturumlari_Scope_LastSeenUtc' AND object_id = OBJECT_ID(N'[dbo].[OnlineKullaniciOturumlari]'))
    CREATE INDEX [IX_OnlineKullaniciOturumlari_Scope_LastSeenUtc] ON [dbo].[OnlineKullaniciOturumlari] ([CompanyId], [BranchId], [LastSeenUtc]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnlineKullaniciOturumlari_PersonelId' AND object_id = OBJECT_ID(N'[dbo].[OnlineKullaniciOturumlari]'))
    CREATE INDEX [IX_OnlineKullaniciOturumlari_PersonelId] ON [dbo].[OnlineKullaniciOturumlari] ([PersonelId]);

IF OBJECT_ID(N'[dbo].[Sayfalar]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[Sayfalar] WHERE [Url] = N'/OnlineUsers/Index')
BEGIN
    INSERT INTO [dbo].[Sayfalar] ([Ad],[Url],[Icon],[ParentId],[Sira],[CreatedDate],[IsActive],[IsDeleted])
    VALUES (N'Çevrimiçi Kullanıcılar',N'/OnlineUsers/Index',N'fa-user-clock',NULL,30,GETDATE(),1,0);
END;

IF OBJECT_ID(N'[dbo].[Sayfalar]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[Sayfalar] WHERE [Url] = N'/OnlineUsers/Index')
BEGIN
    UPDATE [dbo].[Sayfalar]
       SET [Ad] = N'Çevrimiçi Kullanıcılar', [Icon] = N'fa-user-clock', [Sira] = 30, [IsActive] = 1, [IsDeleted] = 0
     WHERE [Url] = N'/OnlineUsers/Index';
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[YetkiSayfalar]', N'U') IS NOT NULL
    DELETE ys FROM [dbo].[YetkiSayfalar] ys INNER JOIN [dbo].[Sayfalar] s ON s.[Id] = ys.[SayfaId] WHERE s.[Url] = N'/OnlineUsers/Index';
IF OBJECT_ID(N'[dbo].[Sayfalar]', N'U') IS NOT NULL
    DELETE FROM [dbo].[Sayfalar] WHERE [Url] = N'/OnlineUsers/Index';
IF OBJECT_ID(N'[dbo].[OnlineKullaniciOturumlari]', N'U') IS NOT NULL
    DROP TABLE [dbo].[OnlineKullaniciOturumlari];
");
        }
    }
}
