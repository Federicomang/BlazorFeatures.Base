using System.Collections.Generic;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureContext
    {
        public List<IBaseFeatureRequest> FeatureChain { get; }
    }
}
