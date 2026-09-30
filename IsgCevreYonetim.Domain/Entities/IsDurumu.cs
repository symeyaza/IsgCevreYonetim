using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class IsDurumu : BaseEntity
{
    [Required, MaxLength(100)] public string Ad { get; set; } = string.Empty;
    [MaxLength(30)] public string Renk { get; set; } = "#64748b";
    public int Sira { get; set; }
    public bool DevamKaydiAcabilir { get; set; }
    public bool Tamamlandi { get; set; }
    public ICollection<TehlikeliIs> TehlikeliIsler { get; set; } = new List<TehlikeliIs>();
}
