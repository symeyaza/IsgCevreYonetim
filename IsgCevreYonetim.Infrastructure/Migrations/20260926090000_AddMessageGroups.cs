using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsgCevreYonetim.Infrastructure.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260926090000_AddMessageGroups")]
public sealed class AddMessageGroups : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("MesajGruplari", t => new {
            Id=t.Column<int>("int", nullable:false).Annotation("SqlServer:Identity", "1, 1"), CompanyId=t.Column<int>("int",nullable:false), BranchId=t.Column<int>("int",nullable:false), KurucuPersonelId=t.Column<int>("int",nullable:false), Ad=t.Column<string>("nvarchar(120)",maxLength:120,nullable:false), OlusturmaUtc=t.Column<DateTime>("datetime2",nullable:false)
        }, constraints: c => c.PrimaryKey("PK_MesajGruplari", x => x.Id));
        m.CreateTable("MesajGrubuUyelikleri", t => new {
            Id=t.Column<int>("int",nullable:false).Annotation("SqlServer:Identity", "1, 1"), GrupId=t.Column<int>("int",nullable:false), PersonelId=t.Column<int>("int",nullable:false), DavetEdenPersonelId=t.Column<int>("int",nullable:false), DavetUtc=t.Column<DateTime>("datetime2",nullable:false), KabulUtc=t.Column<DateTime>("datetime2",nullable:true), RedUtc=t.Column<DateTime>("datetime2",nullable:true), SonOkumaUtc=t.Column<DateTime>("datetime2",nullable:true)
        }, constraints: c => { c.PrimaryKey("PK_MesajGrubuUyelikleri",x=>x.Id); c.ForeignKey("FK_MesajGrubuUyelikleri_MesajGruplari_GrupId",x=>x.GrupId,"MesajGruplari","Id",onDelete:ReferentialAction.Cascade); });
        m.CreateTable("MesajGrubuMesajlari", t => new {
            Id=t.Column<long>("bigint",nullable:false).Annotation("SqlServer:Identity", "1, 1"), GrupId=t.Column<int>("int",nullable:false), GonderenPersonelId=t.Column<int>("int",nullable:false), Icerik=t.Column<string>("nvarchar(2000)",maxLength:2000,nullable:false), GonderimUtc=t.Column<DateTime>("datetime2",nullable:false)
        }, constraints: c => { c.PrimaryKey("PK_MesajGrubuMesajlari",x=>x.Id); c.ForeignKey("FK_MesajGrubuMesajlari_MesajGruplari_GrupId",x=>x.GrupId,"MesajGruplari","Id",onDelete:ReferentialAction.Cascade); });
        m.CreateIndex("IX_MesajGruplari_CompanyId_BranchId","MesajGruplari",new[]{"CompanyId","BranchId"});
        m.CreateIndex("IX_MesajGrubuUyelikleri_GrupId_PersonelId","MesajGrubuUyelikleri",new[]{"GrupId","PersonelId"},unique:true);
        m.CreateIndex("IX_MesajGrubuUyelikleri_PersonelId_KabulUtc_RedUtc","MesajGrubuUyelikleri",new[]{"PersonelId","KabulUtc","RedUtc"});
        m.CreateIndex("IX_MesajGrubuMesajlari_GrupId_Id","MesajGrubuMesajlari",new[]{"GrupId","Id"});
    }
    protected override void Down(MigrationBuilder m) { m.DropTable("MesajGrubuMesajlari"); m.DropTable("MesajGrubuUyelikleri"); m.DropTable("MesajGruplari"); }
}
