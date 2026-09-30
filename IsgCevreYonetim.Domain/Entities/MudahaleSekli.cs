using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class MudahaleSekli : BaseEntity
    {
        [Required, MaxLength(150)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        public int Sira { get; set; }

        public virtual ICollection<IsKazasi> IsKazalari { get; set; } = new List<IsKazasi>();
    }
}
