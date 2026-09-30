using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920013000_AddIsKazasiSahitleri")]
    public partial class AddIsKazasiSahitleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[IsKazasiSahitleri]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IsKazasiSahitleri] (
        [Id] int NOT NULL IDENTITY(1,1),
        [IsKazasiId] int NOT NULL,
        [PersonelId] int NOT NULL,
        [AdSoyad] nvarchar(200) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_IsKazasiSahitleri] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IsKazasiSahitleri_IsKazalari_IsKazasiId]
            FOREIGN KEY ([IsKazasiId]) REFERENCES [dbo].[IsKazalari] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_IsKazasiSahitleri_Personeller_PersonelId]
            FOREIGN KEY ([PersonelId]) REFERENCES [dbo].[Personeller] ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazasiSahitleri_IsKazasiId_PersonelId' AND object_id = OBJECT_ID(N'[dbo].[IsKazasiSahitleri]'))
    CREATE UNIQUE INDEX [IX_IsKazasiSahitleri_IsKazasiId_PersonelId]
        ON [dbo].[IsKazasiSahitleri] ([IsKazasiId], [PersonelId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazasiSahitleri_PersonelId' AND object_id = OBJECT_ID(N'[dbo].[IsKazasiSahitleri]'))
    CREATE INDEX [IX_IsKazasiSahitleri_PersonelId]
        ON [dbo].[IsKazasiSahitleri] ([PersonelId]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[IsKazasiSahitleri]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IsKazasiSahitleri];
");
        }
    }
}
