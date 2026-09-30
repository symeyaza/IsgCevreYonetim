using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsDenetimKaydi : BaseEntity
{
    public int TehlikeliIsId { get; set; }
    public int? TehlikeliIsGunId { get; set; }
    public int? PersonelId { get; set; }
    [Required, MaxLength(100)] public string Islem { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Aciklama { get; set; } = string.Empty;
    [MaxLength(50)] public string? IpAdresi { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    [Required, MaxLength(64)] public string OncekiKayitOzeti { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string KayitOzeti { get; set; } = string.Empty;
    public TehlikeliIs? TehlikeliIs { get; set; }
    public TehlikeliIsGun? Gun { get; set; }
    public Personel? Personel { get; set; }
}
