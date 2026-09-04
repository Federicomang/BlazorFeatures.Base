using BlazorFeatures.Abstractions.Enums;
using System.Collections.ObjectModel;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public class FeatureSystemContainerService : IFeatureRegistry
    {
        private readonly ReadOnlyCollection<FeatureAssemblyDescriptor> _assemblies;
        private readonly ReadOnlyCollection<FeatureDescriptor> _features;
        private readonly ReadOnlyCollection<FeatureDescriptor> _activeFeatures;
        private readonly IReadOnlyDictionary<Type, RenderType> _allFeatures;
        private readonly IReadOnlyDictionary<Assembly, RenderType> _allAssemblies;
        private readonly FeatureTypeResolver _resolver;

        internal FeatureSystemContainerService(
            IEnumerable<FeatureDescriptor> features,
            IEnumerable<FeatureAssemblyDescriptor> assemblies,
            RenderType currentRenderType)
        {
            CurrentRenderType = currentRenderType;

            var featureArray = features
                .OrderBy(feature => feature.ImplementationType.FullName, StringComparer.Ordinal)
                .ThenBy(feature => feature.ContractType.ToString(), StringComparer.Ordinal)
                .ToArray();
            var assemblyArray = assemblies
                .OrderBy(descriptor => descriptor.Assembly.FullName, StringComparer.Ordinal)
                .ToArray();

            _features = Array.AsReadOnly(featureArray);
            _activeFeatures = Array.AsReadOnly(featureArray.Where(feature => feature.IsActive).ToArray());
            _assemblies = Array.AsReadOnly(assemblyArray);
            _allFeatures = new ReadOnlyDictionary<Type, RenderType>(featureArray
                .GroupBy(feature => feature.ImplementationType)
                .ToDictionary(group => group.Key, group => group.First().RenderType));
            _allAssemblies = new ReadOnlyDictionary<Assembly, RenderType>(assemblyArray
                .ToDictionary(descriptor => descriptor.Assembly, descriptor => descriptor.RenderType));
            _resolver = new FeatureTypeResolver(featureArray);
        }

        public RenderType CurrentRenderType { get; }

        public IReadOnlyList<FeatureAssemblyDescriptor> Assemblies => _assemblies;

        public IReadOnlyList<FeatureDescriptor> Features => _features;

        public IReadOnlyList<FeatureDescriptor> ActiveFeatures => _activeFeatures;

        public FeatureResolution Resolve(Type requestType, Type responseType) =>
            _resolver.Resolve(requestType, responseType);

        public IReadOnlyDictionary<Type, RenderType> AllFeatures => _allFeatures;

        public IReadOnlyList<Type> ClientFeatures => [.. _allFeatures
            .Where(feature => feature.Value is RenderType.Client or RenderType.Both)
            .Select(feature => feature.Key)];

        public IReadOnlyList<Type> ServerFeatures => [.. _allFeatures
            .Where(feature => feature.Value is RenderType.Server or RenderType.Both)
            .Select(feature => feature.Key)];

        public IReadOnlyDictionary<Assembly, RenderType> AllAssemblies => _allAssemblies;

        public IReadOnlyList<Assembly> ClientAssemblies => [.. _assemblies
            .Where(descriptor => descriptor.RenderType is RenderType.Client or RenderType.Both)
            .Select(descriptor => descriptor.Assembly)];

        public IReadOnlyList<Assembly> ServerAssemblies => [.. _assemblies
            .Where(descriptor => descriptor.RenderType is RenderType.Server or RenderType.Both)
            .Select(descriptor => descriptor.Assembly)];

        public IReadOnlyList<Type> ClientTypes => [.. _assemblies
            .Where(descriptor => descriptor.RenderType is RenderType.Client or RenderType.Both)
            .SelectMany(descriptor => descriptor.Types)];

        public IReadOnlyList<Type> ServerTypes => [.. _assemblies
            .Where(descriptor => descriptor.RenderType is RenderType.Server or RenderType.Both)
            .SelectMany(descriptor => descriptor.Types)];
    }
}
