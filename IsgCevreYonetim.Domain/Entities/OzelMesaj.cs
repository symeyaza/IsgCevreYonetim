namespace IsgCevreYonetim.Domain.Entities;

public sealed class OzelMesaj
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int GonderenPersonelId { get; set; }
    public int AliciPersonelId { get; set; }
    public string Icerik { get; set; } = string.Empty;
    public DateTime GonderimUtc { get; set; }
    public DateTime? OkunmaUtc { get; set; }
}
