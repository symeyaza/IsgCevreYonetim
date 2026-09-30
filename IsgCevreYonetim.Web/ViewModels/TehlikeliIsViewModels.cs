using IsgCevreYonetim.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Web.ViewModels;

public class TehlikeliIsFormViewModel
{
    public int Id { get; set; }
    [Required, DataType(DataType.Date)] public DateTime Tarih { get; set; } = DateTime.Today;
    [Required, DataType(DataType.Time)] public TimeSpan Saat { get; set; } = DateTime.Now.TimeOfDay;
    [Required] public string CalismaKaynagi { get; set; } = "Dahili";
    public int? CalismaYapacakBirimId { get; set; }
    public int? TaseronFirmaId { get; set; }
    public int? TaseronYetkiliId { get; set; }
    [Required, MaxLength(300)] public string CalismaYapilacakYer { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string YapilacakIsAciklamasi { get; set; } = string.Empty;
    public int? IsDurumuId { get; set; }
    [Required] public int? IsiYaptiranPersonelId { get; set; }
    public int? FirmaSorumlusuPersonelId { get; set; }
    [MaxLength(4000)] public string? Aciklama { get; set; }
    public List<int> IsiYapanPersonelIds { get; set; } = new();
    [MaxLength(200)] public string? TasaronFirmaAdi { get; set; }
    public string? DisCalisanlar { get; set; }
}

public class TehlikeliIsAnaImzaViewModel
{
    public int TehlikeliIsId { get; set; }
    public string? IsiYaptiranImzaData { get; set; }
    public string? FirmaSorumlusuImzaData { get; set; }
    public Dictionary<int, string> KisiImzalari { get; set; } = new();
}

public class TehlikeliIsGunViewModel
{
    public int TehlikeliIsId { get; set; }
    [Range(1, 7)] public int GunNo { get; set; }
    [Required, DataType(DataType.Date)] public DateTime Tarih { get; set; }
    [Required(ErrorMessage = "Kontrol Eden seçilmelidir.")] public int? KontrolEdenPersonelId { get; set; }
    [Required(ErrorMessage = "Onaylayan seçilmelidir.")] public int? OnaylayanPersonelId { get; set; }
    public List<int> SeciliTehlikeSinifiIds { get; set; } = new();
    public List<int> IsiYapanPersonelIds { get; set; } = new();
    public List<int> TaseronCalisanIds { get; set; } = new();
    [MaxLength(200)] public string? TasaronFirmaAdi { get; set; }
    public string? DisCalisanlar { get; set; }
    [MaxLength(50)] public string? VincPlakasi { get; set; }
    [MaxLength(200)] public string? OperatorAdiSoyadi { get; set; }
    [MaxLength(100)] public string? KaziIsMakinesi { get; set; }
    [MaxLength(200)] public string? KaziOperatorAdiSoyadi { get; set; }
    public List<int> SeciliMaddeIds { get; set; } = new();
    [MaxLength(2000)] public string? Aciklama { get; set; }
    public List<IFormFile>? Dosyalar { get; set; }
    public IFormFile? IslakImzaliBelge { get; set; }
    public string? KontrolImzaData { get; set; }
    public string? OnayImzaData { get; set; }
}

public class TehlikeliIsDetayViewModel
{
    public TehlikeliIs Is { get; set; } = null!;
    public IReadOnlyList<TehlikeSinifiMadde> Maddeler { get; set; } = Array.Empty<TehlikeSinifiMadde>();
}
