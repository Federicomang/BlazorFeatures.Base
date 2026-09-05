using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BlazorFeatures.Base.Server
{
    /// <summary>
    /// Immutable metadata exposed to server feature behaviors.
    /// </summary>
    public sealed class ServerFeatureExecutionContext<TResponse> where TResponse : class
    {
        private readonly Func<CancellationToken, ValueTask<ClaimsPrincipal>> _principalFactory;
        private readonly object _principalLock = new();
        private Task<ClaimsPrincipal>? _principalTask;

        public ServerFeatureExecutionContext(
            IBaseFeature feature,
            Type requestType,
            IBaseFeatureRequest<TResponse> request,
            IFeatureContext featureContext,
            ClaimsPrincipal user,
            HttpContext? httpContext)
            : this(
                feature,
                requestType,
                request,
                featureContext,
                CreatePrincipalFactory(user),
                httpContext)
        {
        }

        internal ServerFeatureExecutionContext(
            IBaseFeature feature,
            Type requestType,
            IBaseFeatureRequest<TResponse> request,
            IFeatureContext featureContext,
            Func<CancellationToken, ValueTask<ClaimsPrincipal>> principalFactory,
            HttpContext? httpContext)
        {
            ArgumentNullException.ThrowIfNull(feature);
            ArgumentNullException.ThrowIfNull(requestType);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(featureContext);
            ArgumentNullException.ThrowIfNull(principalFactory);

            Feature = feature;
            RequestType = requestType;
            Request = request;
            FeatureContext = featureContext;
            _principalFactory = principalFactory;
            HttpContext = httpContext;
        }

        public IBaseFeature Feature { get; }

        public Type RequestType { get; }

        public Type ResponseType => typeof(TResponse);

        public IBaseFeatureRequest<TResponse> Request { get; }

        public IFeatureContext FeatureContext { get; }

        public HttpContext? HttpContext { get; }

        /// <summary>
        /// Resolves the principal only when required and caches it for this execution.
        /// </summary>
        public ValueTask<ClaimsPrincipal> GetPrincipalAsync(
            CancellationToken cancellationToken = default)
        {
            lock (_principalLock)
            {
                _principalTask ??= _principalFactory(cancellationToken).AsTask();
                return new ValueTask<ClaimsPrincipal>(_principalTask);
            }
        }

        private static Func<CancellationToken, ValueTask<ClaimsPrincipal>>
            CreatePrincipalFactory(ClaimsPrincipal user)
        {
            ArgumentNullException.ThrowIfNull(user);
            return _ => ValueTask.FromResult(user);
        }
    }
}
