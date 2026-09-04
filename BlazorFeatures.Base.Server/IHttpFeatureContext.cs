using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BlazorFeatures.Base.Server
{
    public interface IHttpFeatureContext : IFeatureContext
    {
        public HttpContext HttpContext { get; }

        public void SetHttpResult(IBaseFeatureRequest owner, IResult result);

        public bool TryGetHttpResult(IBaseFeatureRequest owner, out IResult? result);
    }
}
