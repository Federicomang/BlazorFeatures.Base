using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BlazorFeatures.Base.Handler
{
    [Guid(FeatureSystemHandlerConstants.Client)]
    internal class FeatureSystemHandler(FeatureHandlerParams options) : IFeatureSystemHandler
    {
        public void HandlePolicies(List<(string Name, MethodInfo Builder)> policies)
        {
            if (policies.Count > 0)
            {
                options.Services.AddAuthorizationCore(options =>
                {
                    foreach (var (Name, Builder) in policies)
                    {
                        options.AddPolicy(Name, builder =>
                        {
                            Builder.Invoke(null, [builder]);
                        });
                    }
                });
            }
        }
    }
}
