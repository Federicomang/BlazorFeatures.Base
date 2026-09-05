using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace BlazorFeatures.Base.Server
{
    /// <summary>
    /// Default server-side feature orchestrator. It enforces base authorization,
    /// invokes registered behaviors and finally executes the concrete feature.
    /// </summary>
    public sealed class ServerFeatureService(
        IAuthorizationService authorizationService,
        IFeaturePrincipalProvider principalProvider,
        IEnumerable<IServerFeatureBehavior> behaviors,
        IHttpContextAccessor httpContextAccessor,
        IOptions<FeatureTelemetryOptions> telemetryOptions,
        ILogger<ServerFeatureService> logger) : IServerFeatureService
    {
        private readonly IServerFeatureBehavior[] _behaviors = [.. behaviors
            .OrderBy(behavior => behavior.Order)
            .ThenBy(behavior => behavior.GetType().FullName, StringComparer.Ordinal)];

        public async Task<FeatureResponse<TResponse>> HandleServer<TResponse>(
            IBaseFeature feature,
            IFeatureHandler<TResponse> handler,
            Type requestType,
            IBaseFeatureRequest<TResponse> request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default)
            where TResponse : class
        {
            var httpContext = featureContext is IHttpFeatureContext httpFeatureContext
                ? httpFeatureContext.HttpContext
                : httpContextAccessor.HttpContext;

            try
            {
                var executionContext = new ServerFeatureExecutionContext<TResponse>(
                    feature,
                    requestType,
                    request,
                    featureContext,
                    token => principalProvider.GetPrincipalAsync(featureContext, token),
                    httpContext);

                var authorizationResponse = await AuthorizeAsync(
                    executionContext,
                    cancellationToken);
                if (authorizationResponse != null)
                    return authorizationResponse;

                ServerFeatureDelegate<TResponse> pipeline = () =>
                    handler.Handle(featureContext);

                for (var index = _behaviors.Length - 1; index >= 0; index--)
                {
                    var behavior = _behaviors[index];
                    var next = pipeline;
                    pipeline = () => behavior.HandleAsync(
                        executionContext,
                        next,
                        cancellationToken);
                }

                return await pipeline();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (featureContext is IHttpFeatureContext)
                    throw;

                if (telemetryOptions.Value.IncludeExceptionDetails)
                {
                    logger.LogError(
                        exception,
                        "An error occurred in server feature {FeatureName}. Operation: {OperationId}",
                        requestType.FullName,
                        featureContext.OperationId);
                }
                else
                {
                    logger.LogError(
                        "Server feature {FeatureName} failed with exception type {FeatureExceptionType}. Operation: {OperationId}",
                        requestType.FullName,
                        exception.GetType().FullName,
                        featureContext.OperationId);
                }

                return FeatureResponse<TResponse>.AsFailure(
                    messages: ["Internal server error"],
                    statusCode: HttpStatusCode.InternalServerError);
            }
        }

        private async Task<FeatureResponse<TResponse>?> AuthorizeAsync<TResponse>(
            ServerFeatureExecutionContext<TResponse> executionContext,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            if (executionContext.Feature is not IBaseFeatureAuthorization authorization)
                return null;

            var principal = await executionContext.GetPrincipalAsync(cancellationToken);
            if (!principal.Identities.Any(identity => identity.IsAuthenticated))
            {
                return FeatureResponse<TResponse>.AsFailure(
                    statusCode: HttpStatusCode.Unauthorized);
            }

            var builder = new AuthorizationPolicyBuilder();
            authorization.BuildPolicy(builder);
            var policy = builder.Build();
            var result = await authorizationService.AuthorizeAsync(
                principal,
                policy);

            return result.Succeeded
                ? null
                : FeatureResponse<TResponse>.AsFailure(
                    statusCode: HttpStatusCode.Forbidden);
        }
    }
}
