using System.ComponentModel.DataAnnotations;
using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities;

public class TaseronFirma : BaseEntity
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    [Required, MaxLength(200)] public string FirmaAdi { get; set; } = "";
    [MaxLength(100)] public string? VergiNo { get; set; }
    [MaxLength(100)] public string? Telefon { get; set; }
    public ICollection<TaseronKisi> Kisiler { get; set; } = new List<TaseronKisi>();
}

public class TaseronKisi : BaseEntity
{
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int TaseronFirmaId { get; set; }
    public TaseronFirma? TaseronFirma { get; set; }
    [Required, MaxLength(200)] public string AdSoyad { get; set; } = "";
    [MaxLength(50)] public string? KimlikNo { get; set; }
    [MaxLength(100)] public string? Telefon { get; set; }
    public bool YetkiliMi { get; set; }
    public bool CalisanMi { get; set; } = true;
}
