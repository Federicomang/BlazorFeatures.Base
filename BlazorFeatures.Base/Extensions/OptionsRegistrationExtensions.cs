using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Extensions
{
    internal static class OptionsRegistrationExtensions
    {
        internal static IServiceCollection ConfigureOptionsFromInstance(
            this IServiceCollection services,
            object optionsInstance)
        {
            ArgumentNullException.ThrowIfNull(optionsInstance);

            var optionsType = optionsInstance.GetType();

            var method = typeof(OptionsRegistrationExtensions)
                .GetMethod(
                    nameof(ConfigureOptionsFromInstanceCore),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(optionsType);

            method.Invoke(null, [ services, optionsInstance ]);

            return services;
        }

        private static void ConfigureOptionsFromInstanceCore<TOptions>(
            IServiceCollection services,
            object optionsInstance)
            where TOptions : class
        {
            services
                .AddOptions<TOptions>()
                .Configure(options =>
                {
                    CopyProperties((TOptions)optionsInstance, options);
                });
        }

        private static void CopyProperties<TOptions>(TOptions source, TOptions target)
        {
            foreach (var property in typeof(TOptions).GetProperties())
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;

                property.SetValue(target, property.GetValue(source));
            }
        }
    }
}
