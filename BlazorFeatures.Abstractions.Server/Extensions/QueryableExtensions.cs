using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;

namespace BlazorFeatures.Abstractions.Server.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PagedResponse<T>> ToPaginatedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int count = await source.CountAsync(cancellationToken);
            var items = await PaginateQuery(source, pageNumber, pageSize).ToListAsync(cancellationToken);
            return new PagedResponse<T>()
            {
                Items = items,
                TotalCount = count
            };
        }

        public static IQueryable<T> PaginateQuery<T>(this IQueryable<T> source, int pageNumber, int pageSize) where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pageNumber <= 0 && pageSize <= 0)
            {
                return source;
            }
            else
            {
                return source.Skip(pageNumber * pageSize).Take(pageSize);
            }
        }

        public static IQueryable<K> Filter<T, K>(this IQueryable<T> source, Expression<Func<T, bool>> filter, string? orderStr, Expression<Func<T, K>> selectExpression) where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var query = source.Where(filter);
            List<string> entityOrdering = [], modelOrdering = [];
            if(!string.IsNullOrEmpty(orderStr))
            {
                foreach (var order in orderStr.Split("|"))
                {
                    if (order.StartsWith('$')) modelOrdering.Add(order[1..]);
                    else entityOrdering.Add(order);
                }
            }
            for (var i = 0; i < entityOrdering.Count; i++)
            {
                if(i == 0)
                {
                    query = query.OrderBy(entityOrdering[i]);
                }
                else
                {
                    query = ((IOrderedQueryable<T>)query).ThenBy(entityOrdering[i]);
                }
            }
            var resultQuery = query.Select(selectExpression);
            for (var i = 0; i < modelOrdering.Count; i++)
            {
                if (i == 0)
                {
                    resultQuery = resultQuery.OrderBy(entityOrdering[i]);
                }
                else
                {
                    resultQuery = ((IOrderedQueryable<K>)resultQuery).ThenBy(entityOrdering[i]);
                }
            }
            return resultQuery;
        }
    }
}
