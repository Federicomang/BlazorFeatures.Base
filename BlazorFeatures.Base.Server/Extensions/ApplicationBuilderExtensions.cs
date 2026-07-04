using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseFeatureEndpoints(this IApplicationBuilder applicationBuilder)
        {
            var featureContainerService = applicationBuilder.ApplicationServices.GetRequiredService<FeatureSystemContainerService>();

            var endpointRegisterTypes = featureContainerService.ServerAssemblies.SelectMany(x => x.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IBaseFeatureEndpoint).IsAssignableFrom(type)));

            foreach (var endpointType in endpointRegisterTypes)
            {
                endpointType.GetMethod(nameof(IBaseFeatureEndpoint.MapEndpoints), BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [applicationBuilder]);
            }
        }

        public static void UseFeatures(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseFeatureEndpoints();
        }
    }
}
