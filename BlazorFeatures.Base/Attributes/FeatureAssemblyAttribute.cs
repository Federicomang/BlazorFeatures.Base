using BlazorFeatures.Abstractions.Enums;

namespace BlazorFeatures.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class FeatureAssemblyAttribute(RenderType renderType) : Attribute
    {
        public RenderType RenderType { get; init; } = renderType;
    }
}
