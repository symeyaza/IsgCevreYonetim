namespace IsgCevreYonetim.Shared.DTOs
{
    /// <summary>
    /// Ortak sayfalama görünümünün ihtiyaç duyduğu salt okunur bilgiler.
    /// Generic sonucu object/dynamic kullanmadan güvenli biçimde partial view'a taşır.
    /// </summary>
    public interface IPaginatedResult
    {
        int TotalCount { get; }
        int PageNumber { get; }
        int PageSize { get; }
        int TotalPages { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
    }

    public class PaginatedResult<T> : IPaginatedResult
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0
            ? 0
            : (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
