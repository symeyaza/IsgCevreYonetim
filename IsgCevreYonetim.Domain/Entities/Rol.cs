using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Rol : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        // İlişkiler
        public virtual ICollection<YetkiRol> YetkiRoller { get; set; } = new List<YetkiRol>();
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}