using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Grup : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        // İlişkiler
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}