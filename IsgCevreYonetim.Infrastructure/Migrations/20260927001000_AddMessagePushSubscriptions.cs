using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927001000_AddMessagePushSubscriptions")]
public sealed class AddMessagePushSubscriptions : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("MesajPushAbonelikleri", t => new {
            Id = t.Column<int>("int", nullable:false).Annotation("SqlServer:Identity", "1, 1"),
            PersonelId = t.Column<int>("int", nullable:false),
            Endpoint = t.Column<string>("nvarchar(2048)", maxLength:2048, nullable:false),
            EndpointHash = t.Column<string>("nvarchar(64)", maxLength:64, nullable:false),
            P256dh = t.Column<string>("nvarchar(256)", maxLength:256, nullable:false),
            Auth = t.Column<string>("nvarchar(256)", maxLength:256, nullable:false),
            UpdatedUtc = t.Column<DateTime>("datetime2", nullable:false)
        }, constraints: c => c.PrimaryKey("PK_MesajPushAbonelikleri", x => x.Id));
        m.CreateIndex("IX_MesajPushAbonelikleri_EndpointHash", "MesajPushAbonelikleri", "EndpointHash", unique:true);
        m.CreateIndex("IX_MesajPushAbonelikleri_PersonelId", "MesajPushAbonelikleri", "PersonelId");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("MesajPushAbonelikleri");
}
