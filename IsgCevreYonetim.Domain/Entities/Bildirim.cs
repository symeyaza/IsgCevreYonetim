using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities;

public class Bildirim : BaseEntity
{
    public int PersonelId { get; set; }
    [Required, MaxLength(180)] public string Baslik { get; set; } = string.Empty;
    [Required, MaxLength(600)] public string Mesaj { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Url { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string Tur { get; set; } = string.Empty;
    [Required, MaxLength(180)] public string ReferansAnahtari { get; set; } = string.Empty;
    public bool OkunduMu { get; set; }
    public DateTime? OkunmaTarihi { get; set; }
    public Personel? Personel { get; set; }
}
