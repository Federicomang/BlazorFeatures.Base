using System.Linq.Expressions;

namespace BlazorFeatures.Base.Server.Infrastructure
{
    public class QueryableFilterOptions<T, K> : ICloneable where T : class
    {
        public string ModelOrderingPrefix { get; set; } = "$";

        public string OrderSeparator { get; set; } = "|";

        public Dictionary<string, IOrderExpression<T>?> CustomEntityOrdering { get; set; } = [];

        public Dictionary<string, IOrderExpression<K>?> CustomModelOrdering { get; set; } = [];

        public object Clone()
        {
            return new QueryableFilterOptions<T, K>()
            {
                ModelOrderingPrefix = ModelOrderingPrefix,
                OrderSeparator = OrderSeparator,
                CustomEntityOrdering = CustomEntityOrdering.ToDictionary(),
                CustomModelOrdering = CustomModelOrdering.ToDictionary()
            };
        }
    }

    public interface IOrderExpression<T>
    {
        IOrderedQueryable<T> Apply(IQueryable<T> query, bool descending);
        IOrderedQueryable<T> ApplyThen(IOrderedQueryable<T> query, bool descending);
    }

    public class OrderExpression<T, TKey>(Expression<Func<T, TKey>> expression) : IOrderExpression<T>
    {
        public IOrderedQueryable<T> Apply(IQueryable<T> query, bool descending)
        {
            return descending
                ? query.OrderByDescending(expression)
                : query.OrderBy(expression);
        }

        public IOrderedQueryable<T> ApplyThen(IOrderedQueryable<T> query, bool descending)
        {
            return descending
                ? query.ThenByDescending(expression)
                : query.ThenBy(expression);
        }
    }
}
