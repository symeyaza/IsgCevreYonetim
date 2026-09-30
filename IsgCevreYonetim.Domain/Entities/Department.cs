using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Department : BaseEntity
    {
        public string DepartmentKodu { get; set; } = string.Empty;
        public string DepartmentAdi { get; set; } = string.Empty;

        public int BranchId { get; set; }
        public virtual Branch Branch { get; set; } = null!;

        public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}