using System.Collections.Generic;

namespace BlazorFeatures.Abstractions
{
    public interface IPagedResponse<T>
    {
        public List<T> Items { get; set; }

        public int TotalCount { get; set; }
    }

    public class PagedResponse<T> : IPagedResponse<T>
    {
        public List<T> Items { get; set; } = [];

        public int TotalCount { get; set; }
    }
}
