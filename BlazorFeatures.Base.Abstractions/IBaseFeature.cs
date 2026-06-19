using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public interface IBaseFeature
    {
        public Task<FeatureResponse<object>> HandleClient(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken = default);

        public Task<FeatureResponse<object>> HandleServer(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken = default);
    }
}
