using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927130000_AddMessageAttachments")]
public sealed class AddMessageAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("MesajEkleri", table => new
        {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            OzelMesajId = table.Column<long>("bigint", nullable: true),
            GrupMesajiId = table.Column<long>("bigint", nullable: true),
            DosyaAdi = table.Column<string>("nvarchar(180)", maxLength: 180, nullable: false),
            DepoAdi = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
            IcerikTuru = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            Boyut = table.Column<long>("bigint", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_MesajEkleri", x => x.Id);
            table.ForeignKey("FK_MesajEkleri_OzelMesajlar", x => x.OzelMesajId, "OzelMesajlar", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_MesajEkleri_MesajGrubuMesajlari", x => x.GrupMesajiId, "MesajGrubuMesajlari", "Id", onDelete: ReferentialAction.Cascade);
            table.CheckConstraint("CK_MesajEkleri_TekMesaj", "([OzelMesajId] IS NULL AND [GrupMesajiId] IS NOT NULL) OR ([OzelMesajId] IS NOT NULL AND [GrupMesajiId] IS NULL)");
        });
        migrationBuilder.CreateIndex("IX_MesajEkleri_OzelMesajId", "MesajEkleri", "OzelMesajId");
        migrationBuilder.CreateIndex("IX_MesajEkleri_GrupMesajiId", "MesajEkleri", "GrupMesajiId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("MesajEkleri");
}
