using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiArastirma : BaseEntity
    {
        public int IsKazasiId { get; set; }
        public int? ArastiranPersonelId { get; set; }
        public DateTime ArastirmaTarihi { get; set; } = DateTime.Now;

        public virtual IsKazasi? IsKazasi { get; set; }
        public virtual Personel? ArastiranPersonel { get; set; }
        public virtual ICollection<IsKazasiArastirmaCevap> Cevaplar { get; set; } = new List<IsKazasiArastirmaCevap>();
    }
}
