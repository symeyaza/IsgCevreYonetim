using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class YetkiSayfa : BaseEntity
    {
        public int YetkiId { get; set; }
        public int SayfaId { get; set; }
        public int BranchId { get; set; }
        public bool Ekle { get; set; }
        public bool Guncelle { get; set; }
        public bool Sil { get; set; }
        public bool Goster { get; set; }

        // Navigation properties
        public virtual Yetki Yetki { get; set; } = null!;
        public virtual Sayfa Sayfa { get; set; } = null!;
        public virtual Branch Branch { get; set; } = null!;
    }
}