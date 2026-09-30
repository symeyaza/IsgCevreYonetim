using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiArastirmaKategori : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Aciklama { get; set; }

        public int Sira { get; set; }

        public virtual ICollection<IsKazasiArastirmaMadde> Maddeler { get; set; } = new List<IsKazasiArastirmaMadde>();
    }
}
