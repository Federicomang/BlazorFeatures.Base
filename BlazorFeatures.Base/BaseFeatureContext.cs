using BlazorFeatures.Abstractions;

namespace BlazorFeatures.Base
{
    public class BaseFeatureContext : IFeatureContext
    {
        public List<IBaseFeatureRequest> FeatureChain { get; init; } = [];
    }
}
