using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsGun : BaseEntity
{
    public int TehlikeliIsId { get; set; }
    [Range(1, 7)] public int GunNo { get; set; }
    [Required] public DateTime Tarih { get; set; }
    public int KontrolEdenPersonelId { get; set; }
    public int OnaylayanPersonelId { get; set; }
    [MaxLength(2000)] public string? Aciklama { get; set; }
    [MaxLength(200)] public string? TasaronFirmaUnvani { get; set; }
    [MaxLength(50)] public string? VincPlakasi { get; set; }
    [MaxLength(200)] public string? OperatorAdiSoyadi { get; set; }
    [MaxLength(100)] public string? KaziIsMakinesi { get; set; }
    [MaxLength(200)] public string? KaziOperatorAdiSoyadi { get; set; }
    public DateTime? KontrolSistemOnayTarihi { get; set; }
    public DateTime? OnaySistemOnayTarihi { get; set; }
    [MaxLength(64)] public string? KontrolOnayOzeti { get; set; }
    [MaxLength(64)] public string? OnayOzeti { get; set; }
    [MaxLength(500)] public string? IslakImzaliBelgeYolu { get; set; }
    [MaxLength(64)] public string? IslakImzaliBelgeSha256 { get; set; }
    [MaxLength(500)] public string? KontrolImzaYolu { get; set; }
    [MaxLength(64)] public string? KontrolImzaSha256 { get; set; }
    [MaxLength(500)] public string? OnayImzaYolu { get; set; }
    [MaxLength(64)] public string? OnayImzaSha256 { get; set; }

    public TehlikeliIs? TehlikeliIs { get; set; }
    public Personel? KontrolEdenPersonel { get; set; }
    public Personel? OnaylayanPersonel { get; set; }
    public ICollection<TehlikeliIsGunMadde> Maddeler { get; set; } = new List<TehlikeliIsGunMadde>();
    public ICollection<TehlikeliIsKisi> Kisiler { get; set; } = new List<TehlikeliIsKisi>();
    public ICollection<TehlikeliIsDosya> Dosyalar { get; set; } = new List<TehlikeliIsDosya>();
    public ICollection<TehlikeliIsGunTehlikeSinifi> TehlikeSiniflari { get; set; } = new List<TehlikeliIsGunTehlikeSinifi>();
}
