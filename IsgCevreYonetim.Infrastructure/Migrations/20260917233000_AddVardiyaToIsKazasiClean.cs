using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260917233000_AddVardiyaToIsKazasiClean")]
    public partial class AddVardiyaToIsKazasiClean : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut canlı veritabanını korur: yalnızca eksik Vardiya yapısını ekler.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[Vardiyalar]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Vardiyalar] (
        [VardiyaId] int NOT NULL IDENTITY(1,1),
        [VardiyaAdi] nvarchar(50) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Vardiyalar] PRIMARY KEY ([VardiyaId])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vardiyalar_VardiyaAdi' AND object_id = OBJECT_ID(N'[dbo].[Vardiyalar]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Vardiyalar_VardiyaAdi] ON [dbo].[Vardiyalar] ([VardiyaAdi]);
END;

IF COL_LENGTH('dbo.IsKazalari', 'VardiyaId') IS NULL
BEGIN
    ALTER TABLE [dbo].[IsKazalari] ADD [VardiyaId] int NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazalari_VardiyaId' AND object_id = OBJECT_ID(N'[dbo].[IsKazalari]'))
BEGIN
    CREATE INDEX [IX_IsKazalari_VardiyaId] ON [dbo].[IsKazalari] ([VardiyaId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IsKazalari_Vardiyalar_VardiyaId')
BEGIN
    ALTER TABLE [dbo].[IsKazalari] WITH CHECK ADD CONSTRAINT [FK_IsKazalari_Vardiyalar_VardiyaId]
        FOREIGN KEY([VardiyaId]) REFERENCES [dbo].[Vardiyalar] ([VardiyaId]);
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Vardiyalar] WHERE [VardiyaAdi] = N'08:00 - 16:00')
    INSERT INTO [dbo].[Vardiyalar] ([VardiyaAdi],[CreatedDate],[UpdatedDate],[IsActive],[IsDeleted]) VALUES (N'08:00 - 16:00', GETDATE(), NULL, 1, 0);
IF NOT EXISTS (SELECT 1 FROM [dbo].[Vardiyalar] WHERE [VardiyaAdi] = N'16:00 - 24:00')
    INSERT INTO [dbo].[Vardiyalar] ([VardiyaAdi],[CreatedDate],[UpdatedDate],[IsActive],[IsDeleted]) VALUES (N'16:00 - 24:00', GETDATE(), NULL, 1, 0);
IF NOT EXISTS (SELECT 1 FROM [dbo].[Vardiyalar] WHERE [VardiyaAdi] = N'24:00 - 08:00')
    INSERT INTO [dbo].[Vardiyalar] ([VardiyaAdi],[CreatedDate],[UpdatedDate],[IsActive],[IsDeleted]) VALUES (N'24:00 - 08:00', GETDATE(), NULL, 1, 0);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IsKazalari_Vardiyalar_VardiyaId')
    ALTER TABLE [dbo].[IsKazalari] DROP CONSTRAINT [FK_IsKazalari_Vardiyalar_VardiyaId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazalari_VardiyaId' AND object_id = OBJECT_ID(N'[dbo].[IsKazalari]'))
    DROP INDEX [IX_IsKazalari_VardiyaId] ON [dbo].[IsKazalari];
IF COL_LENGTH('dbo.IsKazalari', 'VardiyaId') IS NOT NULL
    ALTER TABLE [dbo].[IsKazalari] DROP COLUMN [VardiyaId];
IF OBJECT_ID(N'[dbo].[Vardiyalar]', N'U') IS NOT NULL
    DROP TABLE [dbo].[Vardiyalar];
");
        }
    }
}
