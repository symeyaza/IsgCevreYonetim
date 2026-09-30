using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925220000_AddPrivateMessages")]
public sealed class AddPrivateMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OzelMesajlar",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                CompanyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                GonderenPersonelId = table.Column<int>(type: "int", nullable: false),
                AliciPersonelId = table.Column<int>(type: "int", nullable: false),
                Icerik = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                GonderimUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                OkunmaUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            }, constraints: table => table.PrimaryKey("PK_OzelMesajlar", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_OzelMesajlar_CompanyId_BranchId_AliciPersonelId_OkunmaUtc", table: "OzelMesajlar", columns: new[] { "CompanyId", "BranchId", "AliciPersonelId", "OkunmaUtc" });
        migrationBuilder.CreateIndex(name: "IX_OzelMesajlar_CompanyId_BranchId_GonderenPersonelId_GonderimUtc", table: "OzelMesajlar", columns: new[] { "CompanyId", "BranchId", "GonderenPersonelId", "GonderimUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OzelMesajlar");
}
