// Domain/Entities/IsKazasiDosya.cs
using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasiDosya : BaseEntity
    {
        public int IsKazasiId { get; set; }

        [MaxLength(500)]
        public string DosyaAdi { get; set; } = string.Empty;

        [MaxLength(500)]
        public string DosyaYolu { get; set; } = string.Empty;

        [MaxLength(50)]
        public string DosyaTipi { get; set; } = string.Empty; // pdf, image, video

        public long DosyaBoyutu { get; set; }

        public virtual IsKazasi IsKazasi { get; set; } = null!;
    }
}