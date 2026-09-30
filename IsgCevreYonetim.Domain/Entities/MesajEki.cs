namespace IsgCevreYonetim.Domain.Entities;

public sealed class MesajEki
{
    public long Id { get; set; }
    public long? OzelMesajId { get; set; }
    public long? GrupMesajiId { get; set; }
    public OzelMesaj? OzelMesaj { get; set; }
    public MesajGrubuMesaji? GrupMesaji { get; set; }
    public string DosyaAdi { get; set; } = string.Empty;
    public string DepoAdi { get; set; } = string.Empty;
    public string IcerikTuru { get; set; } = string.Empty;
    public long Boyut { get; set; }
}
