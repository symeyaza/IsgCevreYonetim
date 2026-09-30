using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920183000_AddMudahaleSekilleri")]
    public partial class AddMudahaleSekilleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MudahaleSekilleri]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MudahaleSekilleri] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Ad] nvarchar(150) NOT NULL,
        [Aciklama] nvarchar(500) NULL,
        [Sira] int NOT NULL CONSTRAINT [DF_MudahaleSekilleri_Sira] DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_MudahaleSekilleri] PRIMARY KEY ([Id])
    );
END;

-- Eski serbest metin müdahale kayıtlarını kaybetmeden referans tablosuna aktar.
INSERT INTO [dbo].[MudahaleSekilleri]
    ([Ad], [Aciklama], [Sira], [CreatedDate], [UpdatedDate], [IsActive], [IsDeleted])
SELECT DISTINCT LEFT(LTRIM(RTRIM(k.[Mudahale])), 150), NULL, 0, GETDATE(), NULL, 1, 0
FROM [dbo].[IsKazalari] k
WHERE NULLIF(LTRIM(RTRIM(k.[Mudahale])), N'') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[MudahaleSekilleri] m
      WHERE m.[Ad] = LEFT(LTRIM(RTRIM(k.[Mudahale])), 150) AND m.[IsDeleted] = 0
  );

DECLARE @Varsayilanlar TABLE ([Ad] nvarchar(150), [Sira] int);
INSERT INTO @Varsayilanlar ([Ad], [Sira]) VALUES
    (N'İlk Yardım Uygulandı', 10),
    (N'İşyeri Sağlık Birimine Sevk Edildi', 20),
    (N'Hastaneye Sevk Edildi', 30),
    (N'Ambulans ile Hastaneye Sevk Edildi', 40),
    (N'Müdahale Yapılmadı', 50);

INSERT INTO [dbo].[MudahaleSekilleri]
    ([Ad], [Aciklama], [Sira], [CreatedDate], [UpdatedDate], [IsActive], [IsDeleted])
SELECT v.[Ad], NULL, v.[Sira], GETDATE(), NULL, 1, 0
FROM @Varsayilanlar v
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[MudahaleSekilleri] m WHERE m.[Ad] = v.[Ad] AND m.[IsDeleted] = 0);

IF COL_LENGTH(N'dbo.IsKazalari', N'MudahaleSekliId') IS NULL
    ALTER TABLE [dbo].[IsKazalari] ADD [MudahaleSekliId] int NULL;

UPDATE k
SET k.[MudahaleSekliId] = m.[Id]
FROM [dbo].[IsKazalari] k
INNER JOIN [dbo].[MudahaleSekilleri] m
    ON m.[Ad] = LEFT(LTRIM(RTRIM(k.[Mudahale])), 150) AND m.[IsDeleted] = 0
WHERE k.[MudahaleSekliId] IS NULL
  AND NULLIF(LTRIM(RTRIM(k.[Mudahale])), N'') IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MudahaleSekilleri_Ad' AND object_id = OBJECT_ID(N'[dbo].[MudahaleSekilleri]'))
    CREATE UNIQUE INDEX [IX_MudahaleSekilleri_Ad] ON [dbo].[MudahaleSekilleri] ([Ad]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazalari_MudahaleSekliId' AND object_id = OBJECT_ID(N'[dbo].[IsKazalari]'))
    CREATE INDEX [IX_IsKazalari_MudahaleSekliId] ON [dbo].[IsKazalari] ([MudahaleSekliId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IsKazalari_MudahaleSekilleri_MudahaleSekliId')
    ALTER TABLE [dbo].[IsKazalari] ADD CONSTRAINT [FK_IsKazalari_MudahaleSekilleri_MudahaleSekliId]
        FOREIGN KEY ([MudahaleSekliId]) REFERENCES [dbo].[MudahaleSekilleri] ([Id]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_IsKazalari_MudahaleSekilleri_MudahaleSekliId')
    ALTER TABLE [dbo].[IsKazalari] DROP CONSTRAINT [FK_IsKazalari_MudahaleSekilleri_MudahaleSekliId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IsKazalari_MudahaleSekliId' AND object_id = OBJECT_ID(N'[dbo].[IsKazalari]'))
    DROP INDEX [IX_IsKazalari_MudahaleSekliId] ON [dbo].[IsKazalari];
IF COL_LENGTH(N'dbo.IsKazalari', N'MudahaleSekliId') IS NOT NULL
    ALTER TABLE [dbo].[IsKazalari] DROP COLUMN [MudahaleSekliId];
IF OBJECT_ID(N'[dbo].[MudahaleSekilleri]', N'U') IS NOT NULL
    DROP TABLE [dbo].[MudahaleSekilleri];
");
        }
    }
}
