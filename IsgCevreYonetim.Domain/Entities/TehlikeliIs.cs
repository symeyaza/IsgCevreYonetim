using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIs : BaseEntity
{
    [Required] public DateTime Tarih { get; set; }
    [Required] public TimeSpan Saat { get; set; }
    public int BranchId { get; set; }
    public int? CalismaYapacakBirimId { get; set; }
    // Eski kayıtlar dahili departman sayılır; yeni kayıtlarda iki kaynaktan yalnız biri seçilir.
    public int? TaseronFirmaId { get; set; }
    public int? TaseronYetkiliId { get; set; }
    [Required, MaxLength(300)] public string CalismaYapilacakYer { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string YapilacakIsAciklamasi { get; set; } = string.Empty;
    [Range(1, 10000)] public int CalisanPersonelSayisi { get; set; }
    // Eski kayıtlarla uyumluluk için tutulur; yeni seçimler günlük kayıtlarda yapılır.
    public int? TehlikeSinifiId { get; set; }
    public int IsDurumuId { get; set; }
    public int IsiYaptiranPersonelId { get; set; }
    public int? FirmaSorumlusuPersonelId { get; set; }
    public int KontrolEdenPersonelId { get; set; }
    public int OnaylayanPersonelId { get; set; }
    [MaxLength(4000)] public string? Aciklama { get; set; }
    [MaxLength(200)] public string? TasaronFirmaUnvani { get; set; }
    [Required, MaxLength(64)] public string DogrulamaKodu { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(500)] public string? IsiYaptiranImzaYolu { get; set; }
    [MaxLength(64)] public string? IsiYaptiranImzaSha256 { get; set; }
    [MaxLength(500)] public string? FirmaSorumlusuImzaYolu { get; set; }
    [MaxLength(64)] public string? FirmaSorumlusuImzaSha256 { get; set; }
    public DateTime? IsiYaptiranSistemOnayTarihi { get; set; }
    public DateTime? FirmaSorumlusuSistemOnayTarihi { get; set; }
    [MaxLength(64)] public string? IsiYaptiranOnayOzeti { get; set; }
    [MaxLength(64)] public string? FirmaSorumlusuOnayOzeti { get; set; }

    public Branch? Branch { get; set; }
    public Department? CalismaYapacakBirim { get; set; }
    public TaseronFirma? TaseronFirma { get; set; }
    public TaseronKisi? TaseronYetkili { get; set; }
    public TehlikeSinifi? TehlikeSinifi { get; set; }
    public IsDurumu? IsDurumu { get; set; }
    public Personel? IsiYaptiranPersonel { get; set; }
    public Personel? FirmaSorumlusuPersonel { get; set; }
    public Personel? KontrolEdenPersonel { get; set; }
    public Personel? OnaylayanPersonel { get; set; }
    public ICollection<TehlikeliIsKisi> Kisiler { get; set; } = new List<TehlikeliIsKisi>();
    public ICollection<TehlikeliIsGun> Gunler { get; set; } = new List<TehlikeliIsGun>();
    public ICollection<TehlikeliIsDosya> Dosyalar { get; set; } = new List<TehlikeliIsDosya>();
    public ICollection<TehlikeliIsDenetimKaydi> DenetimKayitlari { get; set; } = new List<TehlikeliIsDenetimKaydi>();
}
