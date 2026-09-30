using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public enum DashboardGuncellemeKategori
    {
        [Display(Name = "Genel")]
        Genel = 1,

        [Display(Name = "İş Sağlığı ve Güvenliği")]
        IsSagligiVeGuvenligi = 2,

        [Display(Name = "Çevre")]
        Cevre = 3
    }

    public enum DashboardGuncellemeDurum
    {
        [Display(Name = "Tamamlandı")]
        Tamamlandi = 1,

        [Display(Name = "Devam Ediyor")]
        DevamEdiyor = 2,

        [Display(Name = "Planlanan")]
        Planlanan = 3
    }

    public class DashboardGuncelleme : BaseEntity
    {
        [Required(ErrorMessage = "Sayfa/modül adı zorunludur.")]
        [MaxLength(150)]
        [Display(Name = "Sayfa / Modül Adı")]
        public string SayfaAdi { get; set; } = string.Empty;

        [Required]
        public DashboardGuncellemeKategori Kategori { get; set; }

        [Required]
        public DashboardGuncellemeDurum Durum { get; set; }

        [MaxLength(500)]
        [Display(Name = "Açıklama")]
        public string? Aciklama { get; set; }

        [Range(0, 9999)]
        [Display(Name = "Sıra")]
        public int Sira { get; set; }
    }
}
