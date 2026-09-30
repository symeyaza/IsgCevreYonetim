using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class YetkiRol : BaseEntity
    {
        public int YetkiId { get; set; }
        public int RolId { get; set; }

        // Navigation properties
        public virtual Yetki Yetki { get; set; } = null!;
        public virtual Rol Rol { get; set; } = null!;
    }
}