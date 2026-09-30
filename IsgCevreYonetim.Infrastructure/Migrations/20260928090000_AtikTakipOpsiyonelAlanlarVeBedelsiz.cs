using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928090000_AtikTakipOpsiyonelAlanlarVeBedelsiz")]
public class AtikTakipOpsiyonelAlanlarVeBedelsiz : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [TasimaMotAtNo] nvarchar(100) NULL;
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [IslemeYontemi] nvarchar(200) NULL;
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [OdemeSatis] bit NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE [dbo].[AtikTakipleri] SET [TasimaMotAtNo] = N'' WHERE [TasimaMotAtNo] IS NULL;
UPDATE [dbo].[AtikTakipleri] SET [IslemeYontemi] = N'' WHERE [IslemeYontemi] IS NULL;
UPDATE [dbo].[AtikTakipleri] SET [OdemeSatis] = 0 WHERE [OdemeSatis] IS NULL;
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [TasimaMotAtNo] nvarchar(100) NOT NULL;
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [IslemeYontemi] nvarchar(200) NOT NULL;
ALTER TABLE [dbo].[AtikTakipleri] ALTER COLUMN [OdemeSatis] bit NOT NULL;");
    }
}
