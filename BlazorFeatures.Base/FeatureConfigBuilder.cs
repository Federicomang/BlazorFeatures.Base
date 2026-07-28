using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Options;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public class FeatureConfigBuilder
    {
        internal FeatureConfigBuilder() { }

        internal List<Assembly> Assemblies { get; set; } = [];

        public RenderType ApplicationRenderType { get; set; } = RenderType.Both;

        public Action<IFeatureOptions>? OptionsConfigurator { get; set; }

        public bool RemoveAssembly(Assembly assembly) => Assemblies.Remove(assembly);
        public void AddAssemblies(params Assembly[] assemblies) => Assemblies.AddRange(assemblies);
        public void AddAssemblyContaining<T>() => Assemblies.Add(typeof(T).Assembly);
    }
}
