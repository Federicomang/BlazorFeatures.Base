using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Options;
using BlazorFeatures.Base.Attributes;
using BlazorFeatures.Base.Handler;
using BlazorFeatures.Base.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BlazorFeatures.Base.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddFeatures(this IServiceCollection services, Action<FeatureConfigBuilder>? configure = null)
        {
            var baseFeatureType = typeof(IBaseFeature<,>);
            var featureOptionType = typeof(IFeatureOptions<>);
            var currentRenderType = Constants.IsClientEnvironment
                ? RenderType.Client
                : RenderType.Server;
            var featureHandlerId = Constants.IsClientEnvironment
                ? FeatureSystemHandlerConstants.Client
                : FeatureSystemHandlerConstants.Server;

            var typesWithHandler = new List<Type>();
            var featureRootComponents = new List<Type>();
            var featureOptions = new List<IFeatureOptions>();
            var policies = new List<(string Name, MethodInfo Builder)>();
            var featureRegistrations = new List<(IReadOnlyList<Type> Interfaces, Type Implementation, ServiceLifetime Lifetime)>();
            var featureDescriptors = new List<FeatureDescriptor>();
            var assemblyDescriptors = new List<FeatureAssemblyDescriptor>();
            Type? featureHandlerType = null;

            var builder = new FeatureConfigBuilder
            {
                Assemblies = [.. AppDomain.CurrentDomain.GetAssemblies()]
            };
            configure?.Invoke(builder);

            var assembliesToScan = builder.Assemblies
                .Distinct()
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
                .ToArray();

            foreach (var assembly in assembliesToScan)
            {
                var featureAssembly = assembly.GetCustomAttribute<FeatureAssemblyAttribute>();
                if (featureAssembly == null)
                {
                    if (builder.ExplicitAssemblies.Contains(assembly))
                    {
                        throw new InvalidOperationException(
                            $"Assembly {assembly.FullName} was explicitly added to BlazorFeatures but is not marked " +
                            $"with {nameof(FeatureAssemblyAttribute)}.");
                    }

                    continue;
                }

                var renderType = featureAssembly.RenderType;
                var isActive = renderType == RenderType.Both || renderType == currentRenderType;
                var assemblyTypes = GetAssemblyTypes(assembly);
                assemblyDescriptors.Add(new FeatureAssemblyDescriptor(
                    assembly,
                    renderType,
                    builder.ExplicitAssemblies.Contains(assembly),
                    assemblyTypes));

                foreach (var type in assemblyTypes.Where(type => !type.IsAbstract && !type.IsInterface))
                {
                    var interfaces = type.GetInterfaces();
                    var featureContracts = interfaces
                        .Where(candidate => candidate.IsGenericType
                            && candidate.GetGenericTypeDefinition() == baseFeatureType)
                        .OrderBy(candidate => candidate.ToString(), StringComparer.Ordinal)
                        .ToArray();

                    RegisterPolicies(type, interfaces, policies);

                    if (featureContracts.Length > 0)
                    {
                        var lifetime = type.GetCustomAttribute<FeatureServiceLifetimeAttribute>()?.Lifetime
                            ?? ServiceLifetime.Scoped;
                        if (isActive)
                            ValidateFeatureType(type);

                        foreach (var contract in featureContracts)
                        {
                            var contractArguments = contract.GetGenericArguments();
                            featureDescriptors.Add(new FeatureDescriptor(
                                contract,
                                contractArguments[0],
                                contractArguments[1],
                                type,
                                renderType,
                                lifetime,
                                isActive));
                        }

                        if (isActive)
                        {
                            var otherTypes = type.GetCustomAttribute<FeatureOtherImplementationAttribute>()?.Types ?? [];
                            var registrationInterfaces = featureContracts
                                .Concat(interfaces.Where(otherTypes.Contains))
                                .Distinct()
                                .ToArray();
                            featureRegistrations.Add((registrationInterfaces, type, lifetime));
                        }
                    }

                    if (!isActive)
                        continue;

                    if (interfaces.Contains(typeof(IFeatureRegistrationHandler)))
                        typesWithHandler.Add(type);

                    if (interfaces.Contains(typeof(IFeatureRootComponent)))
                        featureRootComponents.Add(type);

                    if (interfaces.Contains(typeof(IFeatureSystemHandler))
                        && type.GetCustomAttribute<GuidAttribute>()?.Value == featureHandlerId)
                    {
                        if (featureHandlerType != null && featureHandlerType != type)
                        {
                            throw new InvalidOperationException(
                                $"Multiple feature system handlers were found for {currentRenderType}: " +
                                $"{featureHandlerType.FullName}, {type.FullName}.");
                        }

                        featureHandlerType = type;
                    }

                    foreach (var featureOption in interfaces.Where(candidate =>
                        candidate.IsGenericType && candidate.GetGenericTypeDefinition() == featureOptionType))
                    {
                        try
                        {
                            featureOptions.Add((IFeatureOptions)Activator.CreateInstance(type)!);
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException(
                                $"Unable to create feature options type {type.FullName} for {featureOption}.",
                                exception);
                        }
                    }
                }
            }

            ValidatePolicies(policies);

            var containerService = new FeatureSystemContainerService(
                featureDescriptors,
                assemblyDescriptors,
                currentRenderType);
            var featureHandlerParams = new FeatureHandlerParams
            {
                Services = services,
                FeatureContainer = containerService
            };
            var featureHandler = featureHandlerType != null
                ? (IFeatureSystemHandler)Activator.CreateInstance(featureHandlerType, [featureHandlerParams])!
                : null;

            services.AddScoped<IFeatureService, FeatureService>();
            services.AddOptions<FeatureTelemetryOptions>();
            services.AddSingleton(containerService);
            services.AddSingleton<IFeatureRegistry>(containerService);

            foreach (var featureOption in featureOptions)
            {
                builder.OptionsConfigurator?.Invoke(featureOption);
                services.ConfigureOptionsFromInstance(featureOption);
            }

            foreach (var type in typesWithHandler.Distinct())
                FeatureRegistrationHandlerRunner.InvokeBefore(type, services);

            services.AddSingleton(new FeatureRootComponentsManager(featureRootComponents.Distinct().ToList()));
            services.AddCascadingValue(Constants.ApplicationRenderTypeKey, _ => builder.ApplicationRenderType);
            services.AddCascadingValue(Constants.JsonSerializerOptionsKey, serviceProvider =>
                serviceProvider.GetService<IOptions<JsonSerializerOptions>>()?.Value);

            foreach (var (interfaces, implementation, lifetime) in featureRegistrations)
            {
                services.Add(ServiceDescriptor.Describe(implementation, implementation, lifetime));
                if (implementation.IsGenericTypeDefinition)
                    continue;

                services.Add(ServiceDescriptor.Describe(
                    typeof(IBaseFeature),
                    serviceProvider => serviceProvider.GetRequiredService(implementation),
                    lifetime));

                foreach (var featureInterface in interfaces)
                {
                    services.Add(ServiceDescriptor.Describe(
                        featureInterface,
                        serviceProvider => serviceProvider.GetRequiredService(implementation),
                        lifetime));
                }
            }

            featureHandler?.HandlePolicies(policies);

            foreach (var type in typesWithHandler.Distinct())
                FeatureRegistrationHandlerRunner.InvokeAfter(type, services);

            return services;
        }

        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes()
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var loaderErrors = exception.LoaderExceptions
                    .Where(loaderException => loaderException != null)
                    .Select(loaderException => loaderException!.Message)
                    .Distinct()
                    .OrderBy(message => message, StringComparer.Ordinal);

                throw new InvalidOperationException(
                    $"Unable to scan feature assembly {assembly.FullName}:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, loaderErrors),
                    exception);
            }
        }

        private static void ValidateFeatureType(Type featureType)
        {
            if (featureType.ContainsGenericParameters && !featureType.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException(
                    $"Feature type {featureType.FullName} is partially open and cannot be registered.");
            }

            if (featureType.GetConstructors().Length == 0)
            {
                throw new InvalidOperationException(
                    $"Feature type {featureType.FullName} does not expose a public constructor and cannot be created by DI.");
            }
        }

        private static void RegisterPolicies(
            Type type,
            IEnumerable<Type> interfaces,
            ICollection<(string Name, MethodInfo Builder)> policies)
        {
            if (!interfaces.Contains(typeof(IFeaturePolicy)))
                return;

            var method = type.GetMethod(
                nameof(IFeaturePolicy.BuildFeaturePolicy),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (method == null)
            {
                throw new InvalidOperationException(
                    $"Feature policy {type.FullName} does not expose the required public static " +
                    $"{nameof(IFeaturePolicy.BuildFeaturePolicy)} method.");
            }

            policies.Add((FeaturePolicyTools.BuildPolicyName(type), method));
        }

        private static void ValidatePolicies(IEnumerable<(string Name, MethodInfo Builder)> policies)
        {
            var duplicates = policies
                .GroupBy(policy => policy.Name, StringComparer.Ordinal)
                .Where(group => group.Select(policy => policy.Builder.DeclaringType).Distinct().Count() > 1)
                .ToArray();
            if (duplicates.Length == 0)
                return;

            var details = duplicates.Select(group =>
                $"{group.Key}: " + string.Join(", ", group
                    .Select(policy => policy.Builder.DeclaringType?.FullName)
                    .Where(typeName => typeName != null)
                    .Distinct()
                    .OrderBy(typeName => typeName, StringComparer.Ordinal)));

            throw new InvalidOperationException(
                "Multiple feature policies use the same generated name:" + Environment.NewLine +
                string.Join(Environment.NewLine, details));
        }
    }
}
