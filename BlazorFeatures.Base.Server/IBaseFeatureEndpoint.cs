using Microsoft.AspNetCore.Routing;

namespace BlazorFeatures.Base.Server
{
    public interface IBaseFeatureEndpoint
    {
        public static abstract void MapEndpoints(IEndpointRouteBuilder builder);
    }
}
