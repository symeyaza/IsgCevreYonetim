// Domain/Entities/IsKazasi.cs
using IsgCevreYonetim.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace IsgCevreYonetim.Domain.Entities
{
    public class IsKazasi : BaseEntity
    {
        [Required]
        public DateTime KazaTarihi { get; set; }

        [Required]
        public int PersonelId { get; set; }

        // Personel bilgileri (autocomplete ile doldurulacak)
        public string? PersonelAdSoyad { get; set; }

        // Kaza araştırma formunda kullanılan sorumlu amir
        public int? SorumluAmirPersonelId { get; set; }
        [MaxLength(200)] public string? SorumluAmirAdSoyad { get; set; }

        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? UnitId { get; set; }
        public int? GorevId { get; set; }
        public int? GrupId { get; set; }

        public DateTime? IseGirisTarihi { get; set; }

     
        public int? VardiyaId { get; set; }

        [Required, MaxLength(1000)]
        public string Aciklama { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Mudahale { get; set; }

        public int? MudahaleSekliId { get; set; }

        public DateTime? RaporBaslangic { get; set; }
        public DateTime? RaporBitis { get; set; }

        public int? KayipGun { get; set; }

        // Navigation properties
        public virtual Personel? Personel { get; set; }
        public virtual Personel? SorumluAmirPersonel { get; set; }
        public virtual Branch? Branch { get; set; }
        public virtual Department? Department { get; set; }
        public virtual Unit? Unit { get; set; }
        public virtual Gorev? Gorev { get; set; }
        public virtual Grup? Grup { get; set; }
        public virtual Vardiya? Vardiya { get; set; }
        public virtual MudahaleSekli? MudahaleSekli { get; set; }

        // Dosyalar
        public virtual ICollection<IsKazasiDosya> Dosyalar { get; set; } = new List<IsKazasiDosya>();
        public virtual IsKazasiArastirma? Arastirma { get; set; }
        public virtual ICollection<IsKazasiDuzelticiFaaliyet> DuzelticiFaaliyetler { get; set; } = new List<IsKazasiDuzelticiFaaliyet>();
        public virtual ICollection<IsKazasiSahit> Sahitler { get; set; } = new List<IsKazasiSahit>();


        /// <summary>
        /// Kayıp gün hesaplama
        /// Kural:
        /// - Rapor Başlangıç ve Rapor Bitiş dolu ise hesapla
        /// - Rapor Başlangıç == Kaza Tarihi ise: Kayıp Gün = Rapor Bitiş - Rapor Başlangıç
        /// - Rapor Başlangıç > Kaza Tarihi ise: Kayıp Gün = (Rapor Bitiş - Rapor Başlangıç) + 1
        /// </summary>
        public int? HesaplaKayipGun()
        {
            // Rapor başlangıç ve bitiş tarihleri kontrol et
            if (!RaporBaslangic.HasValue || !RaporBitis.HasValue)
                return null;

            // Rapor bitiş tarihi, başlangıçtan önce olamaz
            if (RaporBitis.Value < RaporBaslangic.Value)
                return null;

            // Gün farkını hesapla
            var gunFarki = (RaporBitis.Value - RaporBaslangic.Value).Days;

            // Kaza tarihi ile rapor başlangıç tarihini karşılaştır
            if (RaporBaslangic.Value.Date == KazaTarihi.Date)
            {
                // Aynı gün ise direkt fark
                return gunFarki;
            }
            else if (RaporBaslangic.Value.Date > KazaTarihi.Date)
            {
                // Rapor başlangıç kaza tarihinden sonra ise +1 ekle
                return gunFarki + 1;
            }

            // Rapor başlangıç kaza tarihinden önce ise (normalde olmamalı)
            return gunFarki;
        }
    }
}
