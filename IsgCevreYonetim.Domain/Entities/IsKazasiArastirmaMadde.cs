using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiArastirmaMadde : BaseEntity
    {
        [Required]
        public int KategoriId { get; set; }

        [Required, MaxLength(1000)]
        public string Metin { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Aciklama { get; set; }

        public int Sira { get; set; }

        public virtual IsKazasiArastirmaKategori? Kategori { get; set; }
        public virtual ICollection<IsKazasiArastirmaCevap> Cevaplar { get; set; } = new List<IsKazasiArastirmaCevap>();
    }
}
