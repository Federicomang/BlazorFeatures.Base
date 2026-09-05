using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Handler
{
    internal class FeatureHandlerParams
    {
        public required IServiceCollection Services { get; set; }

        public required FeatureSystemContainerService FeatureContainer { get; set; }
    }
}
