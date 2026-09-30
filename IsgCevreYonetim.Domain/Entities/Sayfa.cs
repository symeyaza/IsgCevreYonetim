using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Sayfa : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Icon { get; set; }

        public int? ParentId { get; set; }
        public int Sira { get; set; }

        // Navigation properties
        public virtual Sayfa? Parent { get; set; }
        public virtual ICollection<Sayfa> SubPages { get; set; } = new List<Sayfa>();
        public virtual ICollection<YetkiSayfa> YetkiSayfalar { get; set; } = new List<YetkiSayfa>();
    }
}