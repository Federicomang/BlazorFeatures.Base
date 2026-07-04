using BlazorFeatures.Base.Attributes;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public interface IFeaturePolicy
    {
        public static abstract void BuildFeaturePolicy(AuthorizationPolicyBuilder builder);
    }

    internal static class FeaturePolicyTools
    {
        internal static string BuildPolicyName(Type type)
        {
            var customName = type.GetCustomAttribute<FeaturePolicyNameAttribute>()?.Name;
            return customName ?? $"FeaturePolicy#{type.Assembly.GetName().Name}#{type.FullName}";
        }
    }
}
