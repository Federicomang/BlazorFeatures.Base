namespace BlazorFeatures.Abstractions
{
    public interface IPagedRequest
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? OrderBy { get; set; }
    }

    public class PagedRequest : IPagedRequest
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? OrderBy { get; set; }
    }
}
