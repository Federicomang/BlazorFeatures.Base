using BlazorFeatures.Abstractions.Enums;
using System.Text.Json;

namespace BlazorFeatures.Abstractions
{
    public class FeatureConfigBuilder
    {
        internal FeatureConfigBuilder() { }

        public JsonSerializerOptions? JsonSerializerOptions { get; set; } = null;

        public RenderType ApplicationRenderType { get; set; } = RenderType.Both;

        internal List<IFeatureOptions> FeatureOptions { get; set; } = [];

        public FeatureConfigBuilder AddOptions<T>(T options) where T : class, IFeatureOptions
        {
            FeatureOptions.Add(options);
            return this;
        }
    }
}
