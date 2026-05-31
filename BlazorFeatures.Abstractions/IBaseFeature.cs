namespace BlazorFeatures.Abstractions
{
    public interface IBaseFeature
    {
        public Task<FeatureResponse<object>> HandleClient(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken = default);

        public Task<FeatureResponse<object>> HandleServer(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken = default);
    }

    public interface IBaseFeature<Request, Response> : IBaseFeature where Request : class, IBaseFeatureRequest<Response> where Response : class
    {
        public Task<FeatureResponse<Response>> HandleClient(Request request, IFeatureContext featureContext, CancellationToken cancellationToken = default);

        public Task<FeatureResponse<Response>> HandleServer(Request request, IFeatureContext featureContext, CancellationToken cancellationToken = default);

        async Task<FeatureResponse<object>> IBaseFeature.HandleClient(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken)
        {
            var res = await HandleClient((Request)request, featureContext, cancellationToken);
            return res.AsGeneric();
        }

        async Task<FeatureResponse<object>> IBaseFeature.HandleServer(IBaseFeatureRequest request, IFeatureContext featureContext, CancellationToken cancellationToken)
        {
            var res = await HandleServer((Request)request, featureContext, cancellationToken);
            return res.AsGeneric();
        }
    }
}
