using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureService
    {
        public Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class;

        public Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>;

        public Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class;

        public Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>;
    }
}
