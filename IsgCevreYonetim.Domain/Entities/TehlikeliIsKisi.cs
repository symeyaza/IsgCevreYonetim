using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsKisi : BaseEntity
{
    public int TehlikeliIsId { get; set; }
    public int? TehlikeliIsGunId { get; set; }
    public int? PersonelId { get; set; }
    public int? TaseronKisiId { get; set; }
    [Required, MaxLength(200)] public string AdSoyad { get; set; } = string.Empty;
    [MaxLength(200)] public string? Firma { get; set; }
    [Required, MaxLength(50)] public string Rol { get; set; } = "IsiYapan";
    [MaxLength(500)] public string? ImzaYolu { get; set; }
    [MaxLength(64)] public string? ImzaSha256 { get; set; }
    public DateTime? SistemOnayTarihi { get; set; }
    public int? OnaylayanPersonelId { get; set; }
    [MaxLength(64)] public string? OnayOzeti { get; set; }
    public TehlikeliIs? TehlikeliIs { get; set; }
    public TehlikeliIsGun? Gun { get; set; }
    public Personel? Personel { get; set; }
    public TaseronKisi? TaseronKisi { get; set; }
    public Personel? OnaylayanPersonel { get; set; }
}
