using System.Collections.Concurrent;

namespace BlazorFeatures.Base
{
    internal sealed class FeatureTypeResolver(IEnumerable<Type> genericFeatureTypes)
    {
        private readonly Type[] _genericFeatureTypes = [.. genericFeatureTypes];
        private readonly ConcurrentDictionary<(Type Request, Type Response), Type> _cache = new();

        public Type Resolve(Type requestType, Type responseType)
        {
            return _cache.GetOrAdd((requestType, responseType), static (key, featureTypes) =>
            {
                var requestedInterface = typeof(IBaseFeature<,>).MakeGenericType(key.Request, key.Response);
                var matches = new List<Type>();

                foreach (var featureType in featureTypes)
                {
                    foreach (var featureInterface in featureType.GetInterfaces()
                        .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IBaseFeature<,>)))
                    {
                        var genericArguments = new Dictionary<Type, Type>();
                        if (!TryMatch(featureInterface, requestedInterface, genericArguments))
                            continue;

                        var featureArguments = featureType.GetGenericArguments();
                        if (featureArguments.Any(x => !genericArguments.ContainsKey(x)))
                            continue;

                        try
                        {
                            var closedFeatureType = featureType.MakeGenericType(
                                [.. featureArguments.Select(x => genericArguments[x])]);

                            if (requestedInterface.IsAssignableFrom(closedFeatureType))
                                matches.Add(closedFeatureType);
                        }
                        catch (ArgumentException)
                        {
                            // The inferred types do not satisfy the generic constraints.
                        }
                    }
                }

                var distinctMatches = matches.Distinct().ToArray();
                return distinctMatches.Length switch
                {
                    1 => distinctMatches[0],
                    0 => throw new InvalidOperationException(
                        $"No generic feature implements {requestedInterface}."),
                    _ => throw new InvalidOperationException(
                        $"Multiple generic features implement {requestedInterface}: " +
                        string.Join(", ", distinctMatches.Select(x => x.FullName)))
                };
            }, _genericFeatureTypes);
        }

        private static bool TryMatch(
            Type template,
            Type concrete,
            Dictionary<Type, Type> genericArguments)
        {
            if (template.IsGenericParameter)
            {
                if (genericArguments.TryGetValue(template, out var currentType))
                    return currentType == concrete;

                genericArguments[template] = concrete;
                return true;
            }

            if (template.IsArray)
            {
                return concrete.IsArray
                    && template.GetArrayRank() == concrete.GetArrayRank()
                    && TryMatch(template.GetElementType()!, concrete.GetElementType()!, genericArguments);
            }

            if (template.IsGenericType)
            {
                if (!concrete.IsGenericType
                    || template.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
                {
                    return false;
                }

                var templateArguments = template.GetGenericArguments();
                var concreteArguments = concrete.GetGenericArguments();
                for (var i = 0; i < templateArguments.Length; i++)
                {
                    if (!TryMatch(templateArguments[i], concreteArguments[i], genericArguments))
                        return false;
                }

                return true;
            }

            return template == concrete;
        }
    }
}
