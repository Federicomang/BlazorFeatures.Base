namespace BlazorFeatures.Abstractions
{
    public class BaseFeatureContext : IFeatureContext
    {
        public List<IBaseFeatureRequest> FeatureChain { get; init; } = [];
    }
}
