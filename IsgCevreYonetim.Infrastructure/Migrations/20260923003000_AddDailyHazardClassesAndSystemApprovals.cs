using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923003000_AddDailyHazardClassesAndSystemApprovals")]
public class AddDailyHazardClassesAndSystemApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF OBJECT_ID(N'[TehlikeliIsGunTehlikeSiniflari]', N'U') IS NULL
        BEGIN
            CREATE TABLE [TehlikeliIsGunTehlikeSiniflari] (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TehlikeliIsGunTehlikeSiniflari] PRIMARY KEY,
                [TehlikeliIsGunId] int NOT NULL,
                [TehlikeSinifiId] int NOT NULL,
                [CreatedDate] datetime2 NOT NULL CONSTRAINT [DF_TIGTS_CreatedDate] DEFAULT GETDATE(),
                [UpdatedDate] datetime2 NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_TIGTS_IsDeleted] DEFAULT 0,
                [IsActive] bit NOT NULL CONSTRAINT [DF_TIGTS_IsActive] DEFAULT 1,
                CONSTRAINT [FK_TIGTS_Gun] FOREIGN KEY ([TehlikeliIsGunId]) REFERENCES [TehlikeliIsGunleri]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_TIGTS_Sinif] FOREIGN KEY ([TehlikeSinifiId]) REFERENCES [TehlikeSiniflari]([Id])
            );
            CREATE UNIQUE INDEX [IX_TIGTS_Gun_Sinif] ON [TehlikeliIsGunTehlikeSiniflari]([TehlikeliIsGunId], [TehlikeSinifiId]);
        END;

        INSERT INTO [TehlikeliIsGunTehlikeSiniflari] ([TehlikeliIsGunId],[TehlikeSinifiId],[CreatedDate],[IsActive],[IsDeleted])
        SELECT g.[Id], i.[TehlikeSinifiId], GETDATE(), 1, 0
        FROM [TehlikeliIsGunleri] g
        JOIN [TehlikeliIsler] i ON i.[Id]=g.[TehlikeliIsId]
        WHERE i.[TehlikeSinifiId] IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM [TehlikeliIsGunTehlikeSiniflari] x WHERE x.[TehlikeliIsGunId]=g.[Id] AND x.[TehlikeSinifiId]=i.[TehlikeSinifiId]);

        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranSistemOnayTarihi') IS NULL ALTER TABLE [TehlikeliIsler] ADD [IsiYaptiranSistemOnayTarihi] datetime2 NULL;
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuSistemOnayTarihi') IS NULL ALTER TABLE [TehlikeliIsler] ADD [FirmaSorumlusuSistemOnayTarihi] datetime2 NULL;
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranOnayOzeti') IS NULL ALTER TABLE [TehlikeliIsler] ADD [IsiYaptiranOnayOzeti] nvarchar(64) NULL;
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuOnayOzeti') IS NULL ALTER TABLE [TehlikeliIsler] ADD [FirmaSorumlusuOnayOzeti] nvarchar(64) NULL;
        IF COL_LENGTH('TehlikeliIsKisileri', 'SistemOnayTarihi') IS NULL ALTER TABLE [TehlikeliIsKisileri] ADD [SistemOnayTarihi] datetime2 NULL;
        IF COL_LENGTH('TehlikeliIsKisileri', 'OnaylayanPersonelId') IS NULL ALTER TABLE [TehlikeliIsKisileri] ADD [OnaylayanPersonelId] int NULL;
        IF COL_LENGTH('TehlikeliIsKisileri', 'OnayOzeti') IS NULL ALTER TABLE [TehlikeliIsKisileri] ADD [OnayOzeti] nvarchar(64) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_TehlikeliIsKisileri_OnaylayanPersonel')
            ALTER TABLE [TehlikeliIsKisileri] ADD CONSTRAINT [FK_TehlikeliIsKisileri_OnaylayanPersonel] FOREIGN KEY ([OnaylayanPersonelId]) REFERENCES [Personeller]([Id]);

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('TehlikeliIsler') AND name='TehlikeSinifiId' AND is_nullable=0)
        BEGIN
            DECLARE @fk nvarchar(128);
            SELECT TOP 1 @fk=fk.name FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id=fkc.constraint_object_id
            JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
            WHERE fk.parent_object_id=OBJECT_ID('TehlikeliIsler') AND c.name='TehlikeSinifiId';
            IF @fk IS NOT NULL EXEC(N'ALTER TABLE [TehlikeliIsler] DROP CONSTRAINT [' + @fk + ']');
            ALTER TABLE [TehlikeliIsler] ALTER COLUMN [TehlikeSinifiId] int NULL;
            ALTER TABLE [TehlikeliIsler] ADD CONSTRAINT [FK_TehlikeliIsler_TehlikeSiniflari_TehlikeSinifiId] FOREIGN KEY ([TehlikeSinifiId]) REFERENCES [TehlikeSiniflari]([Id]);
        END;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF OBJECT_ID(N'[TehlikeliIsGunTehlikeSiniflari]', N'U') IS NOT NULL DROP TABLE [TehlikeliIsGunTehlikeSiniflari];
        IF COL_LENGTH('TehlikeliIsKisileri', 'OnayOzeti') IS NOT NULL ALTER TABLE [TehlikeliIsKisileri] DROP COLUMN [OnayOzeti];
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_TehlikeliIsKisileri_OnaylayanPersonel') ALTER TABLE [TehlikeliIsKisileri] DROP CONSTRAINT [FK_TehlikeliIsKisileri_OnaylayanPersonel];
        IF COL_LENGTH('TehlikeliIsKisileri', 'OnaylayanPersonelId') IS NOT NULL ALTER TABLE [TehlikeliIsKisileri] DROP COLUMN [OnaylayanPersonelId];
        IF COL_LENGTH('TehlikeliIsKisileri', 'SistemOnayTarihi') IS NOT NULL ALTER TABLE [TehlikeliIsKisileri] DROP COLUMN [SistemOnayTarihi];
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuOnayOzeti') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [FirmaSorumlusuOnayOzeti];
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranOnayOzeti') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [IsiYaptiranOnayOzeti];
        IF COL_LENGTH('TehlikeliIsler', 'FirmaSorumlusuSistemOnayTarihi') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [FirmaSorumlusuSistemOnayTarihi];
        IF COL_LENGTH('TehlikeliIsler', 'IsiYaptiranSistemOnayTarihi') IS NOT NULL ALTER TABLE [TehlikeliIsler] DROP COLUMN [IsiYaptiranSistemOnayTarihi];
        """);
}
