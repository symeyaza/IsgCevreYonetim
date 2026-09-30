using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Personel : BaseEntity
    {
        // PersonelKodu kaldırıldı - Id kullanılacak (100000'den başlayacak)

        [Required, MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Soyad { get; set; } = string.Empty;

        [Required, MaxLength(200), EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Sicil No - Unique, zorunlu
        [Required, MaxLength(20)]
        public string SicilNo { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Telefon { get; set; }

        [MaxLength(20)]
        public string? CepTelefonu { get; set; }

        // Cinsiyet - Tablo referansı
        public int? CinsiyetId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DogumTarihi { get; set; }

        public DateTime? IseGirisTarihi { get; set; }

        [MaxLength(500)]
        public string? SifreHash { get; set; }

        [MaxLength(500)]
        public string? SifreSalt { get; set; }

        public bool EmailDogrulandi { get; set; } = false;
        public bool AktifMi { get; set; } = true;

        public DateTime? SonGirisTarihi { get; set; }
        public int BasarisizGirisSayisi { get; set; } = 0;
        public DateTime? KilitlenmeTarihi { get; set; }

        // Login için
        [MaxLength(50)]
        public string? SonGirisIpAdresi { get; set; }

        [MaxLength(500)]
        public string? SonGirisUserAgent { get; set; }

        // Unvan kaldırıldı

        // Profil Resmi
        [MaxLength(500)]
        public string? ProfilResmi { get; set; }

        public int? GorevId { get; set; }
        public int? GrupId { get; set; }

        // IlId ve IlceId kaldırıldı - Personelden il/ilçe bilgisi kaldırıldı

        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? UnitId { get; set; }

        // Personelin doğrudan tek bir yetkisi vardır.
        [Required(ErrorMessage = "Yetki seçimi zorunludur.")]
        public int? YetkiId { get; set; }

        // Navigation properties
        public virtual Company? Company { get; set; }
        public virtual Branch? Branch { get; set; }
        public virtual Department? Department { get; set; }
        public virtual Unit? Unit { get; set; }
        public virtual Gorev? Gorev { get; set; }
        public virtual Grup? Grup { get; set; }
        public virtual Cinsiyet? Cinsiyet { get; set; }

        public virtual Yetki? Yetki { get; set; }

        public string FullName => $"{Ad} {Soyad}";

        [NotMapped]
        public int? Yas => HesaplaYas(DateTime.Today);

        public int? HesaplaYas(DateTime referansTarihi)
        {
            if (!DogumTarihi.HasValue || DogumTarihi.Value.Date > referansTarihi.Date)
                return null;

            var dogum = DogumTarihi.Value.Date;
            var referans = referansTarihi.Date;
            var yas = referans.Year - dogum.Year;

            if (dogum > referans.AddYears(-yas))
                yas--;

            return yas;
        }
    }
}
