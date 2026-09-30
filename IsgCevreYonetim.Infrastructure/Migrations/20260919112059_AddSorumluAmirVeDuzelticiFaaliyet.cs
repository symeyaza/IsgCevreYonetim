using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsgCevreYonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSorumluAmirVeDuzelticiFaaliyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SorumluAmirAdSoyad",
                table: "IsKazalari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SorumluAmirPersonelId",
                table: "IsKazalari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IsKazasiDuzelticiFaaliyetler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsKazasiId = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TamamlanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsKazasiDuzelticiFaaliyetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsKazasiDuzelticiFaaliyetler_IsKazalari_IsKazasiId",
                        column: x => x.IsKazasiId,
                        principalTable: "IsKazalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsKazasiDuzelticiFaaliyetDosyalari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsKazasiDuzelticiFaaliyetId = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DosyaYolu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DosyaTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DosyaBoyutu = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsKazasiDuzelticiFaaliyetDosyalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsKazasiDuzelticiFaaliyetDosyalari_IsKazasiDuzelticiFaaliyetler_IsKazasiDuzelticiFaaliyetId",
                        column: x => x.IsKazasiDuzelticiFaaliyetId,
                        principalTable: "IsKazasiDuzelticiFaaliyetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsKazalari_SorumluAmirPersonelId",
                table: "IsKazalari",
                column: "SorumluAmirPersonelId");

            migrationBuilder.CreateIndex(
                name: "IX_IsKazasiDuzelticiFaaliyetDosyalari_IsKazasiDuzelticiFaaliyetId",
                table: "IsKazasiDuzelticiFaaliyetDosyalari",
                column: "IsKazasiDuzelticiFaaliyetId");

            migrationBuilder.CreateIndex(
                name: "IX_IsKazasiDuzelticiFaaliyetler_IsKazasiId",
                table: "IsKazasiDuzelticiFaaliyetler",
                column: "IsKazasiId");

            migrationBuilder.AddForeignKey(
                name: "FK_IsKazalari_Personeller_SorumluAmirPersonelId",
                table: "IsKazalari",
                column: "SorumluAmirPersonelId",
                principalTable: "Personeller",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IsKazalari_Personeller_SorumluAmirPersonelId",
                table: "IsKazalari");

            migrationBuilder.DropTable(
                name: "IsKazasiDuzelticiFaaliyetDosyalari");

            migrationBuilder.DropTable(
                name: "IsKazasiDuzelticiFaaliyetler");

            migrationBuilder.DropIndex(
                name: "IX_IsKazalari_SorumluAmirPersonelId",
                table: "IsKazalari");

            migrationBuilder.DropColumn(
                name: "SorumluAmirAdSoyad",
                table: "IsKazalari");

            migrationBuilder.DropColumn(
                name: "SorumluAmirPersonelId",
                table: "IsKazalari");
        }
    }
}
