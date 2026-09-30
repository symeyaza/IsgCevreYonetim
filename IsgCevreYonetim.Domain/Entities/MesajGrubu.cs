namespace IsgCevreYonetim.Domain.Entities;

public sealed class MesajGrubu
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int KurucuPersonelId { get; set; }
    public string Ad { get; set; } = "";
    public DateTime OlusturmaUtc { get; set; }
}
public sealed class MesajGrubuUyelik
{
    public int Id { get; set; }
    public int GrupId { get; set; }
    public int PersonelId { get; set; }
    public int DavetEdenPersonelId { get; set; }
    public DateTime DavetUtc { get; set; }
    public DateTime? KabulUtc { get; set; }
    public DateTime? RedUtc { get; set; }
    public DateTime? SonOkumaUtc { get; set; }
}
public sealed class MesajGrubuMesaji
{
    public long Id { get; set; }
    public int GrupId { get; set; }
    public int GonderenPersonelId { get; set; }
    public string Icerik { get; set; } = "";
    public DateTime GonderimUtc { get; set; }
}
