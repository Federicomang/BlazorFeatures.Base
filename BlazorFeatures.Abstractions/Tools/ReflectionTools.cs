using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace BlazorFeatures.Abstractions.Tools
{
    public static class ReflectionTools
    {
        private static readonly ConcurrentDictionary<string, Type> _genericTypeCache = new();
        private static readonly ConcurrentDictionary<string, Func<object[], object>> _createInstanceCache = new();

        public static Type GetGenericType(Type genericType, params Type[] genericParameters)
        {
            var key = $"{genericType.FullName}#{string.Join(',', genericParameters.Select(x => x.FullName))}";
            return _genericTypeCache.GetOrAdd(key, t => genericType.MakeGenericType(genericParameters));
        }

        public static object CreateInstance(Type type, params object[] args)
        {
            var key = $"{type.FullName}#{args.Length}";
            return _createInstanceCache.GetOrAdd(key, _ =>
            {
                var ctor = type.GetConstructors()
                            .FirstOrDefault(c => c.GetParameters().Length == args.Length) ?? throw new InvalidOperationException("No matching constructor");
                var paramExpr = Expression.Parameter(typeof(object[]), "args");
                var argExprs = ctor.GetParameters()
                                   .Select((p, i) =>
                                       Expression.Convert(
                                           Expression.ArrayIndex(paramExpr, Expression.Constant(i)),
                                           p.ParameterType))
                                   .ToArray();

                var newExp = Expression.New(ctor, argExprs);
                var lambda = Expression.Lambda<Func<object[], object>>(newExp, paramExpr);
                return lambda.Compile();
            })(args);
        }
    }
}
