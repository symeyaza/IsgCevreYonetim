using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Vardiya
    {
        [Key]
        public int VardiyaId { get; set; }

        [Required, MaxLength(50)]
        public string VardiyaAdi { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }

        public virtual ICollection<IsKazasi> IsKazalari { get; set; }
            = new List<IsKazasi>();
    }
}