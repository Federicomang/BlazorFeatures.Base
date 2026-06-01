using Microsoft.AspNetCore.Routing;

namespace BlazorFeatures.Abstractions.Server
{
    public interface IBaseFeatureEndpoint
    {
        public static abstract void MapEndpoints(IEndpointRouteBuilder builder);
    }
}
