using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base
{
    public class FeatureService(IServiceProvider serviceProvider) : IFeatureService
    {
        public record EmptyResponse();

        private class ClientHandler<T> : IFeatureHandler<T> where T : class
        {
            private IBaseFeature Feature { get; init; }
            private IBaseFeatureRequest Request { get; init; }
            private CancellationToken CancellationToken { get; init; }

            internal ClientHandler(IBaseFeature feature, IBaseFeatureRequest request, CancellationToken cancellationToken = default)
            {
                Feature = feature;
                Request = request;
                CancellationToken = cancellationToken;
            }

            public async Task<FeatureResponse<T>> Handle(IFeatureContext featureContext)
            {
                featureContext.FeatureChain.Add(Request);
                var res = await Feature.HandleClient(Request, featureContext, CancellationToken);
                return res.ConvertTo<T>();
            }
        }

        private class ServerHandler<T> : IFeatureHandler<T> where T : class
        {
            private IBaseFeature Feature { get; init; }
            private IBaseFeatureRequest Request { get; init; }
            private CancellationToken CancellationToken { get; init; }

            internal ServerHandler(IBaseFeature feature, IBaseFeatureRequest request, CancellationToken cancellationToken = default)
            {
                Feature = feature;
                Request = request;
                CancellationToken = cancellationToken;
            }

            public async Task<FeatureResponse<T>> Handle(IFeatureContext featureContext)
            {
                featureContext.FeatureChain.Add(Request);
                var res = await Feature.HandleServer(Request, featureContext, CancellationToken);
                return res.ConvertTo<T>();
            }
        }

        private async Task<FeatureResponse<Response>> Run<Response>(Type requestType, IBaseFeatureRequest<Response> request, IFeatureContext? featureContext, CancellationToken cancellationToken = default) where Response : class
        {
            var handlerType = ReflectionTools.GetGenericType(typeof(IBaseFeature<,>), requestType, typeof(Response));
            var handler = (IBaseFeature)serviceProvider.GetRequiredService(handlerType);

            if (Constants.IsClientEnvironment)
            {
                var clientHandler = new ClientHandler<Response>(handler, request, cancellationToken);
                return await clientHandler.Handle(featureContext ?? new BaseFeatureContext());
            }
            else
            {
                var serverHandler = new ServerHandler<Response>(handler, request, cancellationToken);
                var serverService = serviceProvider.GetService<IServerFeatureService>();
                if(serverService == null)
                {
                    return await serverHandler.Handle(featureContext ?? new BaseFeatureContext());
                }
                else
                {
                    return await serverService.HandleServer(serverHandler, requestType, request, featureContext, cancellationToken);
                }
            }
        }

        public async Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            return await Run(request.GetType(), request, null, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>
        {
            return await Run(typeof(Request), request, null, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class
        {
            return await Run(request.GetType(), request, featureContext, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>
        {
            return await Run(typeof(Request), request, featureContext, cancellationToken);
        }
    }
}
