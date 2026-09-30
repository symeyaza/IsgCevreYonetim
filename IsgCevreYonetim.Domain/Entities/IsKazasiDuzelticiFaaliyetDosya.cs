using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiDuzelticiFaaliyetDosya : BaseEntity
    {
        public int IsKazasiDuzelticiFaaliyetId { get; set; }
        [MaxLength(500)] public string DosyaAdi { get; set; } = string.Empty;
        [MaxLength(500)] public string DosyaYolu { get; set; } = string.Empty;
        [MaxLength(50)] public string DosyaTipi { get; set; } = string.Empty;
        public long DosyaBoyutu { get; set; }
        public virtual IsKazasiDuzelticiFaaliyet DuzelticiFaaliyet { get; set; } = null!;
    }
}
