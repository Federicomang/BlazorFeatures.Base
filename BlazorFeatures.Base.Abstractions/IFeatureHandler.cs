using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureHandler<T> where T : class
    {
        public Task<FeatureResponse<T>> Handle(IFeatureContext featureContext);
    }
}
