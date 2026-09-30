using IsgCevreYonetim.Shared.Entities;

namespace IsgCevreYonetim.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string CompanyKodu { get; set; } = string.Empty;
        public string CompanyAdi { get; set; } = string.Empty;
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? LogoUrl { get; set; }

        // İlişkiler
        public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
        public virtual ICollection<Personel> Personeller { get; set; } = new List<Personel>();
    }
}