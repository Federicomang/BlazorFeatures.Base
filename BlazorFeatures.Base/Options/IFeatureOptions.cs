using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Options;

namespace BlazorFeatures.Base.Options
{
    public interface IFeatureOptions<T> : IFeatureOptions where T : class, IBaseFeature, new()
    {
        Type IFeatureOptions.FeatureType => typeof(T);
    }
}
