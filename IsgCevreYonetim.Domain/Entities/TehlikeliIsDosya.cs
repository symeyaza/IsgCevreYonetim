using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsDosya : BaseEntity
{
    public int TehlikeliIsId { get; set; }
    public int? TehlikeliIsGunId { get; set; }
    [Required, MaxLength(260)] public string DosyaAdi { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string DosyaYolu { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string DosyaTipi { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string Sha256 { get; set; } = string.Empty;
    public long DosyaBoyutu { get; set; }
    public TehlikeliIs? TehlikeliIs { get; set; }
    public TehlikeliIsGun? Gun { get; set; }
}
