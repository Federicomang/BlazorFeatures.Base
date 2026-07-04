using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace BlazorFeatures.Base.Handler
{
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FeatureHandlerParams
    {
        public required IServiceCollection Services { get; set; }

        public required FeatureSystemContainerService FeatureContainer { get; set; }
    }
}
