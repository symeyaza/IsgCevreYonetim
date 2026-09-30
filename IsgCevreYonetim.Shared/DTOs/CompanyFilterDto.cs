namespace IsgCevreYonetim.Shared.DTOs
{
    public class CompanyFilterDto
    {
        public string? SearchTerm { get; set; }
        public int? CompanyId { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
    }
}