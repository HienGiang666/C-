namespace TourApp.CMS.Helpers
{
    public class PaginationHelper
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public IEnumerable<T> Paginate<T>(IEnumerable<T> items)
        {
            return items
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);
        }
    }

    public class SearchFilter
    {
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; } = "Id";
        public bool SortDescending { get; set; } = false;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
