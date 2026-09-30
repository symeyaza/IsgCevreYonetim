// Shared/DTOs/IsKazasiDto.cs
namespace IsgCevreYonetim.Shared.DTOs
{
    public class IsKazasiDto
    {
        public int Id { get; set; }
        public DateTime KazaTarihi { get; set; }
        public int PersonelId { get; set; }
        public string? PersonelAdSoyad { get; set; }
        public int? BranchId { get; set; }
        public string? BranchAdi { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentAdi { get; set; }
        public int? UnitId { get; set; }
        public string? UnitAdi { get; set; }
        public int? GorevId { get; set; }
        public string? GorevAdi { get; set; }
        public int? GrupId { get; set; }
        public string? GrupAdi { get; set; }
        public DateTime? IseGirisTarihi { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public string? Mudahale { get; set; }
        public DateTime? RaporBaslangic { get; set; }
        public DateTime? RaporBitis { get; set; }
        public int? KayipGun { get; set; }
        public List<IsKazasiDosyaDto> Dosyalar { get; set; } = new();
        public DateTime CreatedDate { get; set; }
    }

    public class IsKazasiDosyaDto
    {
        public int Id { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string DosyaYolu { get; set; } = string.Empty;
        public string DosyaTipi { get; set; } = string.Empty;
        public long DosyaBoyutu { get; set; }
    }

    public class IsKazasiFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
        public int? PersonelId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? KayipGunMin { get; set; }
        public int? KayipGunMax { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
    }

    public class PersonelAutocompleteDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string? BranchAdi { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentAdi { get; set; }
        public int? UnitId { get; set; }
        public string? UnitAdi { get; set; }
        public int? GorevId { get; set; }
        public string? GorevAdi { get; set; }
        public int? GrupId { get; set; }
        public string? GrupAdi { get; set; }
        public DateTime? DogumTarihi { get; set; }
        public DateTime? IseGirisTarihi { get; set; }
    }
}
