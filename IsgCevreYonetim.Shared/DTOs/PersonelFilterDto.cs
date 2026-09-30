namespace IsgCevreYonetim.Shared.DTOs
{
    public class PersonelFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
        public bool? IsActive { get; set; }
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? UnitId { get; set; }
        public int? YetkiId { get; set; }
    }
}