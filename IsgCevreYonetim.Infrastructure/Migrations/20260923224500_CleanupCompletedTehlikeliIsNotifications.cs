using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923224500_CleanupCompletedTehlikeliIsNotifications")]
public class CleanupCompletedTehlikeliIsNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF OBJECT_ID(N'[dbo].[Bildirimler]', N'U') IS NOT NULL
        BEGIN
            DELETE b FROM [dbo].[Bildirimler] b
            LEFT JOIN [dbo].[TehlikeliIsler] i
                ON b.[ReferansAnahtari] LIKE CONCAT(N'tehlikeli-is:',i.[Id],N':%')
            WHERE b.[ReferansAnahtari] LIKE N'tehlikeli-is:%'
              AND (i.[Id] IS NULL OR i.[IsDeleted]=1);

            DELETE b FROM [dbo].[Bildirimler] b
            JOIN [dbo].[TehlikeliIsler] i
                ON b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',i.[Id],N':yaptiran')
            WHERE i.[IsiYaptiranSistemOnayTarihi] IS NOT NULL;

            DELETE b FROM [dbo].[Bildirimler] b
            JOIN [dbo].[TehlikeliIsler] i
                ON b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',i.[Id],N':firma')
            WHERE i.[FirmaSorumlusuSistemOnayTarihi] IS NOT NULL;

            DELETE b FROM [dbo].[Bildirimler] b
            JOIN [dbo].[TehlikeliIsGunleri] g
                ON b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':kontrol')
            WHERE g.[IsDeleted]=1 OR g.[KontrolSistemOnayTarihi] IS NOT NULL;

            DELETE b FROM [dbo].[Bildirimler] b
            JOIN [dbo].[TehlikeliIsGunleri] g
                ON b.[ReferansAnahtari]=CONCAT(N'tehlikeli-is:',g.[TehlikeliIsId],N':gun:',g.[GunNo],N':onay')
            WHERE g.[IsDeleted]=1 OR g.[OnaySistemOnayTarihi] IS NOT NULL;
        END;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
