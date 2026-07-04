using BlazorFeatures.Abstractions.Enums;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public class FeatureSystemContainerService
    {
        private Dictionary<Type, RenderType> PrivAllFeatures { get; init; }
        private Dictionary<Assembly, RenderType> PrivAllAssemblies { get; init; }

        internal FeatureSystemContainerService(Dictionary<Type, RenderType> dict, Dictionary<Assembly, RenderType> assemblyRenders)
        {
            PrivAllFeatures = dict;
            PrivAllAssemblies = assemblyRenders;
        }

        public IReadOnlyDictionary<Type, RenderType> AllFeatures => PrivAllFeatures.ToDictionary();

        public IReadOnlyList<Type> ClientFeatures => [.. PrivAllFeatures.Where(x => x.Value == RenderType.Client || x.Value == RenderType.Both).Select(x => x.Key)];

        public IReadOnlyList<Type> ServerFeatures => [.. PrivAllFeatures.Where(x => x.Value == RenderType.Server || x.Value == RenderType.Both).Select(x => x.Key)];

        public IReadOnlyDictionary<Assembly, RenderType> AllAssemblies => PrivAllAssemblies.ToDictionary();

        public IReadOnlyList<Assembly> ClientAssemblies => [.. PrivAllAssemblies.Where(x => x.Value == RenderType.Client || x.Value == RenderType.Both).Select(x => x.Key)];

        public IReadOnlyList<Assembly> ServerAssemblies => [.. PrivAllAssemblies.Where(x => x.Value == RenderType.Server || x.Value == RenderType.Both).Select(x => x.Key)];
    }
}
