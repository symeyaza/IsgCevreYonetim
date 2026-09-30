using System.ComponentModel.DataAnnotations;
using IsgCevreYonetim.Shared.Entities;
namespace IsgCevreYonetim.Domain.Entities;
public class AtikTuru : BaseEntity { public int BranchId { get; set; } [Required,MaxLength(120)] public string AtikTurAdi { get; set; } = ""; }
public class AtikFirmaTuru : BaseEntity { public int BranchId { get; set; } [Required,MaxLength(120)] public string AtikFirmaTuruAdi { get; set; } = ""; }
public class AtikFirma : BaseEntity { public int BranchId { get; set; } [Required,MaxLength(200)] public string AtikFirmaAdi { get; set; } = ""; [MaxLength(120)] public string? LisansNo { get; set; } [Required] public int AtikFirmaTuruId { get; set; } public AtikFirmaTuru? AtikFirmaTuru { get; set; } }
public class Atik : BaseEntity { public int BranchId { get; set; } [Required,MaxLength(200)] public string AtikAdi { get; set; } = ""; [Required,MaxLength(30)] public string AtikKodu { get; set; } = ""; [Required] public int AtikTuruId { get; set; } public AtikTuru? AtikTuru { get; set; } }
public class AtikTakibi : BaseEntity {
 public int BranchId { get; set; }
 [MaxLength(100)] public string? TasimaMotAtNo { get; set; }
 public DateTime Tarih { get; set; } = DateTime.Today;
 [Required] public int AtikId { get; set; } public Atik? Atik { get; set; }
 [Range(typeof(decimal),"0.001","999999999999")] public decimal Miktar { get; set; }
 // false: ödeme/bertaraf, true: satış, null: bedelsiz
 public bool? OdemeSatis { get; set; }
 [Range(typeof(decimal),"0","999999999999")] public decimal BertarafBedeli { get; set; }
 [Range(typeof(decimal),"0","999999999999")] public decimal NakliyeBedeli { get; set; }
 [MaxLength(200)] public string? IslemeYontemi { get; set; }
 public decimal HesaplananTutar { get; set; }
 [Required] public int TasiyiciFirmaId { get; set; } public AtikFirma? TasiyiciFirma { get; set; }
 [Required] public int AliciFirmaId { get; set; } public AtikFirma? AliciFirma { get; set; }
 [MaxLength(120)] public string? AliciLisansNo { get; set; }
 public ICollection<AtikTakipDosyasi> Dosyalar { get; set; } = new List<AtikTakipDosyasi>();
}
public class AtikTakipDosyasi : BaseEntity { public int AtikTakibiId { get; set; } public AtikTakibi? AtikTakibi { get; set; } [Required,MaxLength(255)] public string DosyaAdi { get; set; } = ""; [Required,MaxLength(150)] public string IcerikTuru { get; set; } = ""; [Required,MaxLength(100)] public string SaklamaAdi { get; set; } = ""; public long Boyut { get; set; } }
