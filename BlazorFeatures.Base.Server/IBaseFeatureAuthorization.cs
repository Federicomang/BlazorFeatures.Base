using Microsoft.AspNetCore.Authorization;

namespace BlazorFeatures.Base.Server
{
    public interface IBaseFeatureAuthorization
    {
        public void BuildPolicy(AuthorizationPolicyBuilder policy);
    }
}
