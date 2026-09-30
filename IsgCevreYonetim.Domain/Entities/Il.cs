using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Il : BaseEntity
    {
        [Required, MaxLength(50)]
        public string Kod { get; set; } = string.Empty; // Plaka kodu (34, 06, vb.)

        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        // İlişkiler
        public virtual ICollection<Ilce> Ilceler { get; set; } = new List<Ilce>();
        public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
    }
}