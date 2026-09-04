using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class ServerFeatureSystemExtensions
    {
        public static void AddToDbContext(this FeatureSystemContainerService containerService, ModelBuilder modelBuilder)
        {
            var dbContextBuilders = containerService.ServerTypes
                .Where(type => !type.IsAbstract
                    && !type.IsInterface
                    && typeof(IDbContextExtension).IsAssignableFrom(type));

            foreach (var builder in dbContextBuilders)
            {
                builder.GetMethod(nameof(IDbContextExtension.OnModelCreating), BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [modelBuilder]);
            }
        }
    }
}
