using BlazorFeatures.Abstractions.Enums;
using System.Text.Json;

namespace BlazorFeatures.Abstractions
{
    public class FeatureApplicationOptions
    {
        public JsonSerializerOptions? JsonSerializerOptions { get; internal set; } = null;

        public RenderType ApplicationRenderType { get; internal set; } = RenderType.Both;
    }
}
