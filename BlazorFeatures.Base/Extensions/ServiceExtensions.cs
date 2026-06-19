using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Options;
using BlazorFeatures.Base.Attributes;
using BlazorFeatures.Base.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

namespace BlazorFeatures.Base.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddFeatures(this IServiceCollection services, Action<FeatureConfigBuilder>? configure = null)
        {
            var baseFeatureType = typeof(IBaseFeature<,>);
            var featureOptionType = typeof(IFeatureOptions<>);
            var currentRenderType = Constants.IsClientEnvironment ? RenderType.Client : RenderType.Server;
            var typesWithHandler = new List<Type>();
            var featureRootComponents = new List<Type>();
            var featureOptions = new List<IFeatureOptions>();
            var featureTypes = new List<(List<Type> Interfaces, Type Implementation, ServiceLifetime Lifetime)>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var renderType = assembly.GetCustomAttribute<FeatureAssemblyAttribute>()?.RenderType;
                if (renderType == RenderType.Both || renderType == currentRenderType)
                {
                    foreach (var type in assembly.GetTypes()) {
                        if(!type.IsAbstract && !type.IsInterface)
                        {
                            var interfaces = type.GetInterfaces();
                            List<Type> allInterfaces = [];
                            var otherTypes = type.GetCustomAttribute<FeatureOtherImplementationAttribute>()?.Types ?? [];
                            var serviceLifetime = type.GetCustomAttribute<FeatureServiceLifetimeAttribute>()?.Lifetime ?? ServiceLifetime.Scoped;
                            var isFeature = false;
                            foreach (var i in interfaces)
                            {
                                if (i == typeof(IFeatureRegistrationHandler))
                                {
                                    typesWithHandler.Add(type);
                                }
                                if (i == typeof(IFeatureRootComponent))
                                {
                                    featureRootComponents.Add(type);
                                }
                                if (i.IsGenericType && i.GetGenericTypeDefinition() == featureOptionType)
                                {
                                    featureOptions.Add((IFeatureOptions)Activator.CreateInstance(type)!);
                                }
                                if (i.IsGenericType && i.GetGenericTypeDefinition() == baseFeatureType)
                                {
                                    isFeature = true;
                                    allInterfaces.Add(i);
                                }
                                else if (otherTypes.Contains(i))
                                {
                                    allInterfaces.Add(i);
                                }
                            }
                            if (!isFeature)
                            {
                                allInterfaces.Clear();
                            }
                            featureTypes.Add((Interfaces: allInterfaces, Implementation: type, Lifetime: serviceLifetime));
                        }
                    }
                }
            }

            var builder = new FeatureConfigBuilder();
            configure?.Invoke(builder);

            foreach (var featureOption in featureOptions)
            {
                builder.OptionsConfigurator?.Invoke(featureOption);
                services.ConfigureOptionsFromInstance(featureOption);
            }

            foreach (var type in typesWithHandler)
            {
                FeatureRegistrationHandlerRunner.InvokeBefore(type, services);
            }

            services.AddScoped<IFeatureService, FeatureService>();
            services.AddSingleton(new FeatureRootComponentsManager(featureRootComponents));
            services.AddCascadingValue(Constants.ApplicationRenderTypeKey, sp => builder.ApplicationRenderType);
            services.AddCascadingValue(Constants.JsonSerializerOptionsKey, sp => sp.GetService<IOptions<JsonSerializerOptions>>()?.Value);

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
