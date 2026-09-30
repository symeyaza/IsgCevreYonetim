using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsGunMadde : BaseEntity
{
    public int TehlikeliIsGunId { get; set; }
    public int MaddeId { get; set; }
    public bool Uygun { get; set; }
    public TehlikeliIsGun? Gun { get; set; }
    public TehlikeSinifiMadde? Madde { get; set; }
}
