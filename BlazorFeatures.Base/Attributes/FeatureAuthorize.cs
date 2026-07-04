using Microsoft.AspNetCore.Authorization;

namespace BlazorFeatures.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class FeaturePolicyAuthorizeAttribute<T> : AuthorizeAttribute where T : class, IFeaturePolicy
    {
        public FeaturePolicyAuthorizeAttribute()
        {
            Policy = FeaturePolicyTools.BuildPolicyName(typeof(T));
        }
    }
}
