using BlazorFeatures.Abstractions;

namespace BlazorFeatures.Base.Server
{
    public delegate Task<FeatureResponse<TResponse>> ServerFeatureDelegate<TResponse>()
        where TResponse : class;

    /// <summary>
    /// Adds an optional layer around server-side feature execution. Mandatory
    /// IBaseFeatureAuthorization checks always run before this pipeline.
    /// </summary>
    public interface IServerFeatureBehavior
    {
        int Order { get; }

        Task<FeatureResponse<TResponse>> HandleAsync<TResponse>(
            ServerFeatureExecutionContext<TResponse> context,
            ServerFeatureDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
            where TResponse : class;
    }
}
