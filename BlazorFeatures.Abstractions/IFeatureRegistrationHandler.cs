using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureRegistrationHandler
    {
        public static abstract void BeforeRegistration(IServiceCollection services);

        public static abstract void AfterRegistration(IServiceCollection services);
    }

    internal static class FeatureRegistrationHandlerRunner
    {
        internal static void InvokeBefore(Type type, IServiceCollection services)
        {
            var method = typeof(FeatureRegistrationHandlerRunner)
                .GetMethod(nameof(InvokeBeforeGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type);

            method.Invoke(null, [services]);
        }

        internal static void InvokeAfter(Type type, IServiceCollection services)
        {
            var method = typeof(FeatureRegistrationHandlerRunner)
                .GetMethod(nameof(InvokeAfterGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type);

            method.Invoke(null, [services]);
        }

        private static void InvokeBeforeGeneric<T>(IServiceCollection services)
            where T : IFeatureRegistrationHandler
        {
            T.BeforeRegistration(services);
        }

        private static void InvokeAfterGeneric<T>(IServiceCollection services)
            where T : IFeatureRegistrationHandler
        {
            T.AfterRegistration(services);
        }
    }
}
