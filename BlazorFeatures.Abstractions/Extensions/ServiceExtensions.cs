using BlazorFeatures.Abstractions.Attributes;
using BlazorFeatures.Abstractions.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace BlazorFeatures.Abstractions.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddFeatures(this IServiceCollection services, Action<FeatureConfigBuilder>? configure = null)
        {
            services.AddScoped<IFeatureService, FeatureService>();

            var builder = new FeatureConfigBuilder();

            configure?.Invoke(builder);

            var options = new FeatureApplicationOptions()
            {
                ApplicationRenderType = builder.ApplicationRenderType,
                JsonSerializerOptions = builder.JsonSerializerOptions ?? JsonSerializerOptions.Default
            };

            services.AddSingleton(options);
            services.AddCascadingValue(Constants.ApplicationRenderTypeKey, sp => options.ApplicationRenderType);
            services.AddCascadingValue(Constants.JsonSerializerOptionsKey, sp => options.JsonSerializerOptions);

            foreach (var option in builder.FeatureOptions)
            {
                services.AddSingleton(option);
                services.AddSingleton(option.GetType(), option);
            }

            var baseFeatureType = typeof(IBaseFeature<,>);
            var currentRenderType = Constants.IsClientEnvironment ? RenderType.Client : RenderType.Server;
            var typesWithHandler = new List<Type>();
            var featureTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x =>
                {
                    var renderType = x.GetCustomAttribute<FeatureAssemblyAttribute>()?.RenderType;
                    return renderType == RenderType.Both || renderType == currentRenderType;
                })
                .SelectMany(assembly =>
                    assembly.GetTypes().Where(type => !type.IsAbstract && !type.IsInterface).Select(type =>
                    {
                        var interfaces = type.GetInterfaces();
                        List<Type> allInterfaces = [];
                        var otherTypes = type.GetCustomAttribute<FeatureOtherImplementationAttribute>()?.Types ?? [];
                        var serviceLifetime = type.GetCustomAttribute<FeatureServiceLifetimeAttribute>()?.Lifetime ?? ServiceLifetime.Scoped;
                        var isFeature = false;
                        foreach (var i in interfaces)
                        {
                            if (i.IsGenericType && i.GetGenericTypeDefinition() == baseFeatureType)
                            {
                                isFeature = true;
                                allInterfaces.Add(i);
                            }
                            else if (otherTypes.Contains(i))
                            {
                                allInterfaces.Add(i);
                            }
                            if(i == typeof(IFeatureRegistrationHandler))
                            {
                                typesWithHandler.Add(type);
                            }
                        }
                        if (!isFeature)
                        {
                            allInterfaces.Clear();
                        }
                        return (Interfaces: allInterfaces, Implementation: type, Lifetime: serviceLifetime);
                    })
            );

            foreach(var type in typesWithHandler)
            {
                FeatureRegistrationHandlerRunner.InvokeBefore(type, services);
            }

            foreach (var (Interfaces, Implementation, Lifetime) in featureTypes)
            {
                if (Interfaces.Count > 0)
                {
                    services.Add(ServiceDescriptor.Describe(Implementation, Implementation, Lifetime));
                    services.Add(ServiceDescriptor.Describe(typeof(IBaseFeature), sp => sp.GetRequiredService(Implementation), Lifetime));
                    foreach (var i in Interfaces)
                    {
                        services.Add(ServiceDescriptor.Describe(i, sp => sp.GetRequiredService(Implementation), Lifetime));
                    }
                }
            }

            foreach (var type in typesWithHandler)
            {
                FeatureRegistrationHandlerRunner.InvokeAfter(type, services);
            }

            return services;
        }
    }
}
