using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using Microsoft.AspNetCore.Http;

namespace BlazorFeatures.Base.Server
{
    public sealed class HttpFeatureContext : BaseFeatureContext, IHttpFeatureContext
    {
        private readonly Dictionary<IBaseFeatureRequest, IResult> _customResults;

        public HttpContext HttpContext { get; }

        public HttpFeatureContext(HttpContext httpContext, Guid? operationId = null)
            : base(FeatureInvocationSource.Http, operationId)
        {
            HttpContext = httpContext;
            _customResults = new(ReferenceEqualityComparer.Instance);
            httpContext.Features.Set<IHttpFeatureContext>(this);
        }

        private HttpFeatureContext(HttpFeatureContext parent)
            : base(parent)
        {
            HttpContext = parent.HttpContext;
            _customResults = parent._customResults;
        }

        public override IFeatureContext CreateInvocationScope() =>
            new HttpFeatureContext(this);

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
