using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities;

public class TehlikeliIsGunTehlikeSinifi : BaseEntity
{
    public int TehlikeliIsGunId { get; set; }
    public int TehlikeSinifiId { get; set; }
    public TehlikeliIsGun? Gun { get; set; }
    public TehlikeSinifi? TehlikeSinifi { get; set; }
}
