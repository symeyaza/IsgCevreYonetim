using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class YetkiSube : BaseEntity
    {
        public int YetkiId { get; set; }
        public int BranchId { get; set; }
        public bool LokasyonSecebilir { get; set; }

        public virtual Yetki Yetki { get; set; } = null!;
        public virtual Branch Branch { get; set; } = null!;
    }
}
