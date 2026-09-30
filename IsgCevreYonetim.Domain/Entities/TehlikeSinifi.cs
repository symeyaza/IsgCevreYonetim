using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeSinifi : BaseEntity
{
    [Required, MaxLength(150)] public string Ad { get; set; } = string.Empty;
    [MaxLength(500)] public string? Aciklama { get; set; }
    public int Sira { get; set; }
    public ICollection<TehlikeSinifiMadde> Maddeler { get; set; } = new List<TehlikeSinifiMadde>();
    public ICollection<TehlikeliIs> TehlikeliIsler { get; set; } = new List<TehlikeliIs>();
}
