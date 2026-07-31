using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Server.Infrastructure;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PagedResponse<T>> ToPaginatedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where T : class
        {
            ArgumentNullException.ThrowIfNull(source);
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
            ArgumentNullException.ThrowIfNull(source);
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
            => Filter(source, filter, orderStr, selectExpression, new());

        public static IQueryable<K> Filter<T, K>(this IQueryable<T> source, Expression<Func<T, bool>> filter, string? orderStr, Expression<Func<T, K>> selectExpression, QueryableFilterOptions<T, K> options) where T : class
        {
            ArgumentNullException.ThrowIfNull(source);
            var query = source.Where(filter);
            List<string> entityOrdering = [], modelOrdering = [];
            if (!string.IsNullOrEmpty(orderStr))
            {
                foreach (var order in orderStr.Split(options.OrderSeparator))
                {
                    if (order.StartsWith(options.ModelOrderingPrefix)) modelOrdering.Add(order[1..]);
                    else entityOrdering.Add(order);
                }
            }
            var alreadyOrdered = false;
            for (var i = 0; i < entityOrdering.Count; i++)
            {
                var item = entityOrdering[i].Trim();
                var idx = item.IndexOf(' ');
                var descending = false;
                bool existsCustomOrdering;
                IOrderExpression<T>? customOrdering;
                if (idx < 0)
                {
                    existsCustomOrdering = options.CustomEntityOrdering.TryGetValue(item, out customOrdering);
                }
                else
                {
                    var name = item[..idx];
                    var sortDir = item[(idx + 1)..];
                    existsCustomOrdering = options.CustomEntityOrdering.TryGetValue(name, out customOrdering);
                    descending = "desc".Equals(sortDir, StringComparison.InvariantCultureIgnoreCase);
                }
                if (alreadyOrdered)
                {
                    if (existsCustomOrdering)
                    {
                        if(customOrdering != null)
                        {
                            query = customOrdering.ApplyThen((IOrderedQueryable<T>)query, descending);
                        }
                    }
                    else
                    {
                        query = ((IOrderedQueryable<T>)query).ThenBy(item);
                    }
                }
                else
                {
                    if (existsCustomOrdering)
                    {
                        if(customOrdering != null)
                        {
                            query = customOrdering.Apply(query, descending);
                            alreadyOrdered = true;
                        }
                    }
                    else
                    {
                        query = query.OrderBy(item);
                        alreadyOrdered = true;
                    }
                }
            }
            var resultQuery = query.Select(selectExpression);
            alreadyOrdered = false;
            for (var i = 0; i < modelOrdering.Count; i++)
            {
                var item = modelOrdering[i].Trim();
                var idx = item.IndexOf(' ');
                var descending = false;
                bool existsCustomOrdering;
                IOrderExpression<K>? customOrdering;
                if (idx < 0)
                {
                    existsCustomOrdering = options.CustomModelOrdering.TryGetValue(item, out customOrdering);
                }
                else
                {
                    var name = item[..idx];
                    var sortDir = item[(idx + 1)..];
                    existsCustomOrdering = options.CustomModelOrdering.TryGetValue(name, out customOrdering);
                    descending = "desc".Equals(sortDir, StringComparison.InvariantCultureIgnoreCase);
                }
                if (alreadyOrdered)
                {
                    if (existsCustomOrdering)
                    {
                        if(customOrdering != null)
                        {
                            resultQuery = customOrdering.ApplyThen((IOrderedQueryable<K>)resultQuery, descending);
                        }
                    }
                    else
                    {
                        resultQuery = ((IOrderedQueryable<K>)resultQuery).ThenBy(item);
                    }
                }
                else
                {
                    if (existsCustomOrdering)
                    {
                        if(customOrdering != null)
                        {
                            resultQuery = customOrdering.Apply(resultQuery, descending);
                            alreadyOrdered = true;
                        }
                    }
                    else
                    {
                        resultQuery = resultQuery.OrderBy(item);
                        alreadyOrdered = true;
                    }
                }
            }
            return resultQuery;
        }
    }
}
