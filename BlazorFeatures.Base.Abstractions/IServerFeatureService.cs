using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public interface IServerFeatureService
    {
        public Task<FeatureResponse<Response>> HandleServer<Response>(IFeatureHandler<Response> handler, Type requestType, IBaseFeatureRequest<Response> request, IFeatureContext? featureContext, CancellationToken cancellationToken = default) where Response : class;
    }
}
