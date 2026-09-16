using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace BlazorFeatures.Base.Server
{
    public sealed class HttpFeatureContext : BaseFeatureContext, IHttpFeatureContext
    {
        private readonly ConcurrentDictionary<IBaseFeatureRequest, IResult> _customResults;

        public HttpContext HttpContext { get; }

        public HttpFeatureContext(HttpContext httpContext, IBaseFeatureRequest request, Guid? operationId = null)
            : base(request, FeatureInvocationSource.Http, operationId)
        {
            HttpContext = httpContext;
            _customResults = new(ReferenceEqualityComparer.Instance);
            httpContext.Features.Set<IHttpFeatureContext>(this);
        }

        private HttpFeatureContext(IBaseFeatureRequest request, HttpFeatureContext parent)
            : base(request, parent)
        {
            HttpContext = parent.HttpContext;
            _customResults = parent._customResults;
        }

        public override IFeatureContext CreateInvocationScope(IBaseFeatureRequest request) =>
            new HttpFeatureContext(request, this);

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
