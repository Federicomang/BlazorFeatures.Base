using Microsoft.AspNetCore.Authorization;

namespace BlazorFeatures.Abstractions.Server
{
    public interface IBaseFeatureAuthorization
    {
        public void BuildPolicy(AuthorizationPolicyBuilder policy);
    }
}
