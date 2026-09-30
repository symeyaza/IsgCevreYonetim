using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Unit : BaseEntity
    {
        public string UnitKodu { get; set; } = string.Empty;
        public string UnitAdi { get; set; } = string.Empty;

        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; } = null!;

        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}