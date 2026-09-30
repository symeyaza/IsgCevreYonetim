using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Yetki : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        // İlişkiler
        public virtual ICollection<YetkiSayfa> YetkiSayfalar { get; set; } = new List<YetkiSayfa>();
        public virtual ICollection<YetkiSube> YetkiSubeler { get; set; } = new List<YetkiSube>();
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}