using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace BlazorFeatures.Base.Server.Options
{
    public interface IFeatureBaseServerOptions
    {
        public Action<AuthorizationPolicyBuilder, bool>? AuthPolicy { get; set; }

        public Func<string, RouteHandlerBuilder, RouteHandlerBuilder>? EndpointBuilder { get; set; }
    }
}
