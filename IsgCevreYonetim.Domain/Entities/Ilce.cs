using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Ilce : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        // İlişkiler
        public int IlId { get; set; }
        public virtual Il Il { get; set; } = null!;

        public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
    }
}