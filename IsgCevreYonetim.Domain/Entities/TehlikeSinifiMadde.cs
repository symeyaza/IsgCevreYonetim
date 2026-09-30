using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeSinifiMadde : BaseEntity
{
    public int TehlikeSinifiId { get; set; }
    [Required, MaxLength(1200)] public string Metin { get; set; } = string.Empty;
    public int Sira { get; set; }
    public int? ExcelSatirNo { get; set; }
    public TehlikeSinifi? TehlikeSinifi { get; set; }
    public ICollection<TehlikeliIsGunMadde> GunMaddeleri { get; set; } = new List<TehlikeliIsGunMadde>();
}
