using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiDuzelticiFaaliyet : BaseEntity
    {
        public int IsKazasiId { get; set; }
        [Required, MaxLength(200)] public string Baslik { get; set; } = string.Empty;
        [Required, MaxLength(2000)] public string Aciklama { get; set; } = string.Empty;
        public DateTime? TamamlanmaTarihi { get; set; }
        public virtual IsKazasi IsKazasi { get; set; } = null!;
        public virtual ICollection<IsKazasiDuzelticiFaaliyetDosya> Dosyalar { get; set; } = new List<IsKazasiDuzelticiFaaliyetDosya>();
    }
}
