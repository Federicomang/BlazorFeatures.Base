using BlazorFeatures.Abstractions.Enums;

namespace BlazorFeatures.Base
{
    public interface IFeatureRegistry
    {
        public RenderType CurrentRenderType { get; }

        public IReadOnlyList<FeatureAssemblyDescriptor> Assemblies { get; }

        public IReadOnlyList<FeatureDescriptor> Features { get; }

        public IReadOnlyList<FeatureDescriptor> ActiveFeatures { get; }

        public FeatureResolution Resolve(Type requestType, Type responseType);
    }
}
