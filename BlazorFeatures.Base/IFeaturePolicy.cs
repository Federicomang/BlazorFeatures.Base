using BlazorFeatures.Base.Attributes;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public interface IFeaturePolicy
    {
        public static abstract void BuildFeaturePolicy(AuthorizationPolicyBuilder builder);
    }

    public static class FeaturePolicyTools
    {
        public static string BuildPolicyName(Type type)
        {
            var customName = type.GetCustomAttribute<FeaturePolicyNameAttribute>()?.Name;
            return customName ?? $"FeaturePolicy#{type.Assembly.GetName().Name}#{type.FullName}";
        }

        public static string BuildPolicyName<T>() where T : class, IFeaturePolicy => BuildPolicyName(typeof(T));
    }
}
