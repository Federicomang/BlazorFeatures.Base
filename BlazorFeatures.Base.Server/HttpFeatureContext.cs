using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using Microsoft.AspNetCore.Http;

namespace BlazorFeatures.Base.Server
{
    public sealed class HttpFeatureContext : BaseFeatureContext, IHttpFeatureContext
    {
        private readonly Dictionary<IBaseFeatureRequest, IResult> _customResults =
            new(ReferenceEqualityComparer.Instance);

        public HttpContext HttpContext { get; }

        public HttpFeatureContext(HttpContext httpContext, Guid? operationId = null)
            : base(FeatureInvocationSource.Http, operationId)
        {
            HttpContext = httpContext;
            httpContext.Features.Set<IHttpFeatureContext>(this);
        }

        public void SetHttpResult(IBaseFeatureRequest owner, IResult result)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(result);
            _customResults[owner] = result;
        }

        public bool TryGetHttpResult(IBaseFeatureRequest owner, out IResult? result)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return _customResults.TryGetValue(owner, out result);
        }
    }
}
