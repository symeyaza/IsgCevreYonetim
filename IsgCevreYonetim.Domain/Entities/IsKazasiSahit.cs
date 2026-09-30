using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiSahit : BaseEntity
    {
        public int IsKazasiId { get; set; }
        public int PersonelId { get; set; }

        [Required, MaxLength(200)]
        public string AdSoyad { get; set; } = string.Empty;

        public virtual IsKazasi? IsKazasi { get; set; }
        public virtual Personel? Personel { get; set; }
    }
}
