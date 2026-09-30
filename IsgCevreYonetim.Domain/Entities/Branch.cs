using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Branch : BaseEntity
    {
        public string BranchKodu { get; set; } = string.Empty;
        public string BranchAdi { get; set; } = string.Empty;

        // SGK Bilgileri
        public string? SgkIsyeriSicilNo { get; set; }
        public string? SgkIsyeriKodu { get; set; }

        // Vergi Bilgileri
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }

        // İletişim Bilgileri
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? PostaKodu { get; set; }

        // Yetkili Kişi Bilgileri
        public string? YetkiliKisi { get; set; }
        public string? YetkiliTelefon { get; set; }

        // Lokasyon
        public int? IlId { get; set; }
        public int? IlceId { get; set; }

        public int CompanyId { get; set; }
        public virtual Company Company { get; set; } = null!;

        // Navigation properties
        public virtual Il? Il { get; set; }
        public virtual Ilce? Ilce { get; set; }

        public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}