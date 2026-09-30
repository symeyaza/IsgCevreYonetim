using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiArastirmaCevap : BaseEntity
    {
        public int IsKazasiArastirmaId { get; set; }
        public int MaddeId { get; set; }

        public virtual IsKazasiArastirma? Arastirma { get; set; }
        public virtual IsKazasiArastirmaMadde? Madde { get; set; }
    }
}
