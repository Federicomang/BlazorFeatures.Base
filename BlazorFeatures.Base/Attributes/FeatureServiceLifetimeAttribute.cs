using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class FeatureServiceLifetimeAttribute(ServiceLifetime serviceLifetime) : Attribute
    {
        public ServiceLifetime Lifetime { get; init; } = serviceLifetime;
    }
}
