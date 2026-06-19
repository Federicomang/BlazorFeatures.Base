using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Options;

namespace BlazorFeatures.Base
{
    public class FeatureConfigBuilder
    {
        internal FeatureConfigBuilder() { }

        public RenderType ApplicationRenderType { get; set; } = RenderType.Both;

        public Action<IFeatureOptions>? OptionsConfigurator { get; set; }
    }
}
