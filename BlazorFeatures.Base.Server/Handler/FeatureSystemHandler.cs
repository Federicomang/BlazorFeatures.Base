using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BlazorFeatures.Base.Server.Handler
{
    [Guid(FeatureSystemHandlerConstants.Server)]
    internal class FeatureSystemHandler(FeatureHandlerParams options) : IFeatureSystemHandler
    {
        public void HandlePolicies(List<(string Name, MethodInfo Builder)> policies)
        {
            options.Services.AddAuthorization(authorizationOptions =>
            {
                foreach (var (Name, Builder) in policies)
                {
                    authorizationOptions.AddPolicy(Name, builder =>
                    {
                        Builder.Invoke(null, [builder]);
                    });
                }
            });

            options.Services.AddHttpContextAccessor();
            options.Services.TryAddScoped<IFeaturePrincipalProvider, DefaultFeaturePrincipalProvider>();
            options.Services.TryAddScoped<IServerFeatureService, ServerFeatureService>();
        }
    }
}
