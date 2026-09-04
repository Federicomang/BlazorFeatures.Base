using System.Collections.Concurrent;

namespace BlazorFeatures.Base
{
    internal sealed class FeatureTypeResolver
    {
        private readonly FeatureDescriptor[] _activeFeatures;
        private readonly FeatureDescriptor[] _genericFeatures;
        private readonly IReadOnlyDictionary<(Type Request, Type Response), FeatureDescriptor> _exactFeatures;
        private readonly ConcurrentDictionary<(Type Request, Type Response), FeatureResolution> _cache = new();

        internal FeatureTypeResolver(IEnumerable<FeatureDescriptor> features)
        {
            _activeFeatures = [.. features
                .Where(feature => feature.IsActive)
                .OrderBy(feature => feature.ImplementationType.FullName, StringComparer.Ordinal)
                .ThenBy(feature => feature.ContractType.ToString(), StringComparer.Ordinal)];

            ValidateDescriptors(_activeFeatures);

            _genericFeatures = [.. _activeFeatures.Where(feature => feature.IsOpenGeneric)];
            _exactFeatures = _activeFeatures
                .Where(feature => !feature.IsOpenGeneric)
                .ToDictionary(
                    feature => (feature.RequestType, feature.ResponseType),
                    feature => feature);
        }

        public FeatureResolution Resolve(Type requestType, Type responseType)
        {
            ArgumentNullException.ThrowIfNull(requestType);
            ArgumentNullException.ThrowIfNull(responseType);

            return _cache.GetOrAdd((requestType, responseType), static (key, resolver) =>
            {
                if (resolver._exactFeatures.TryGetValue(key, out var exactFeature))
                {
                    return new FeatureResolution(exactFeature, exactFeature.ImplementationType);
                }

                var requestedInterface = typeof(IBaseFeature<,>).MakeGenericType(key.Request, key.Response);
                var matches = new List<(FeatureDescriptor Descriptor, Type Implementation)>();

                foreach (var descriptor in resolver._genericFeatures)
                {
                    var genericArguments = new Dictionary<Type, Type>();
                    if (!TryMatch(descriptor.ContractType, requestedInterface, genericArguments))
                        continue;

                    var featureArguments = descriptor.ImplementationType.GetGenericArguments();
                    if (featureArguments.Any(argument => !genericArguments.ContainsKey(argument)))
                        continue;

                    try
                    {
                        var closedFeatureType = descriptor.ImplementationType.MakeGenericType(
                            [.. featureArguments.Select(argument => genericArguments[argument])]);

                        if (requestedInterface.IsAssignableFrom(closedFeatureType))
                            matches.Add((descriptor, closedFeatureType));
                    }
                    catch (ArgumentException)
                    {
                        // The inferred types do not satisfy the generic constraints.
                    }
                }

                var distinctMatches = matches
                    .GroupBy(match => match.Implementation)
                    .Select(group => group.First())
                    .OrderBy(match => match.Implementation.FullName, StringComparer.Ordinal)
                    .ToArray();

                return distinctMatches.Length switch
                {
                    1 => new FeatureResolution(distinctMatches[0].Descriptor, distinctMatches[0].Implementation),
                    0 => throw BuildNotFoundException(requestedInterface, resolver._activeFeatures),
                    _ => throw BuildAmbiguousException(requestedInterface, distinctMatches)
                };
            }, this);
        }

        private static void ValidateDescriptors(IEnumerable<FeatureDescriptor> descriptors)
        {
            var descriptorArray = descriptors.ToArray();
            var duplicateGroups = descriptorArray
                .Where(feature => !feature.IsOpenGeneric)
                .GroupBy(feature => (feature.RequestType, feature.ResponseType))
                .Where(group => group.Select(feature => feature.ImplementationType).Distinct().Count() > 1)
                .ToArray();

            if (duplicateGroups.Length > 0)
            {
                var details = duplicateGroups.Select(group =>
                    $"{FormatContract(group.Key.RequestType, group.Key.ResponseType)}:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, group
                        .Select(feature => $"  - {FormatType(feature.ImplementationType)} ({feature.Assembly.GetName().Name})")
                        .Distinct()
                        .OrderBy(value => value, StringComparer.Ordinal)));

                throw new InvalidOperationException(
                    "Multiple active features implement the same closed contract:" +
                    Environment.NewLine + string.Join(Environment.NewLine, details));
            }

            foreach (var descriptor in descriptorArray.Where(feature => feature.IsOpenGeneric))
            {
                var implementationArguments = descriptor.ImplementationType.GetGenericArguments();
                var uninferredArguments = implementationArguments
                    .Where(argument => !ContainsGenericParameter(descriptor.ContractType, argument))
                    .Select(argument => argument.Name)
                    .ToArray();

                if (uninferredArguments.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"The generic feature {FormatType(descriptor.ImplementationType)} cannot be resolved from " +
                        $"{descriptor.ContractType}. The following generic arguments cannot be inferred: " +
                        string.Join(", ", uninferredArguments));
                }
            }
        }

        private static InvalidOperationException BuildNotFoundException(
            Type requestedInterface,
            IEnumerable<FeatureDescriptor> activeFeatures)
        {
            var candidates = activeFeatures
                .Select(feature => $"  - {FormatType(feature.ImplementationType)} as {feature.ContractType}")
                .Distinct()
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            var candidateText = candidates.Length == 0
                ? "  (no active features were registered)"
                : string.Join(Environment.NewLine, candidates);

            return new InvalidOperationException(
                $"No active feature implements {requestedInterface}. Registered candidates:{Environment.NewLine}" +
                candidateText);
        }

        private static InvalidOperationException BuildAmbiguousException(
            Type requestedInterface,
            IEnumerable<(FeatureDescriptor Descriptor, Type Implementation)> matches)
        {
            var details = matches.Select(match =>
                $"  - {FormatType(match.Implementation)} from template {match.Descriptor.ContractType}");

            return new InvalidOperationException(
                $"Multiple generic features implement {requestedInterface}:{Environment.NewLine}" +
                string.Join(Environment.NewLine, details));
        }

        private static string FormatContract(Type requestType, Type responseType) =>
            $"IBaseFeature<{FormatType(requestType)}, {FormatType(responseType)}>";

        private static string FormatType(Type type) => type.FullName ?? type.ToString();

        private static bool ContainsGenericParameter(Type type, Type genericParameter)
        {
            if (type == genericParameter)
                return true;

            if (type.HasElementType)
                return ContainsGenericParameter(type.GetElementType()!, genericParameter);

            return type.IsGenericType
                && type.GetGenericArguments().Any(argument => ContainsGenericParameter(argument, genericParameter));
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
                for (var index = 0; index < templateArguments.Length; index++)
                {
                    if (!TryMatch(templateArguments[index], concreteArguments[index], genericArguments))
                        return false;
                }

                return true;
            }

            return template == concrete;
        }
    }
}
