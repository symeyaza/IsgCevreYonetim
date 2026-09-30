namespace IsgCevreYonetim.Shared.DTOs
{
    public class DepartmentFilterDto
    {
        public string? SearchTerm { get; set; }
        public int? BranchId { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
    }
}