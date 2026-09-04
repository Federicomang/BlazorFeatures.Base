using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Options;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public class FeatureConfigBuilder
    {
        internal FeatureConfigBuilder() { }

        internal List<Assembly> Assemblies { get; set; } = [];

        internal HashSet<Assembly> ExplicitAssemblies { get; } = [];

        public RenderType ApplicationRenderType { get; set; } = RenderType.Both;

        public Action<IFeatureOptions>? OptionsConfigurator { get; set; }

        public bool RemoveAssembly(Assembly assembly)
        {
            ExplicitAssemblies.Remove(assembly);
            return Assemblies.RemoveAll(candidate => candidate == assembly) > 0;
        }

        public void AddAssemblies(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            foreach (var assembly in assemblies)
            {
                ArgumentNullException.ThrowIfNull(assembly);
                Assemblies.Add(assembly);
                ExplicitAssemblies.Add(assembly);
            }
        }

        public void AddAssemblyContaining<T>() => AddAssemblies(typeof(T).Assembly);
    }
}
