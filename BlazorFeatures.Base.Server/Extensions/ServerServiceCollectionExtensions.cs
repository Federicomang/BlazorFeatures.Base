using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class ServerServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a server feature behavior once. Lower Order values execute first.
        /// </summary>
        public static IServiceCollection AddServerFeatureBehavior<TBehavior>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TBehavior : class, IServerFeatureBehavior
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IServerFeatureBehavior),
                typeof(TBehavior),
                lifetime));
            return services;
        }
    }
}
