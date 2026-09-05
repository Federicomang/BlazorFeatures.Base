using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BlazorFeatures.Base.Server
{
    internal sealed class DefaultFeaturePrincipalProvider(
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor) : IFeaturePrincipalProvider
    {
        public async ValueTask<ClaimsPrincipal> GetPrincipalAsync(
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default)
        {
            if (featureContext is IHttpFeatureContext httpFeatureContext)
                return httpFeatureContext.HttpContext.User;

            var authenticationStateProvider =
                serviceProvider.GetService<AuthenticationStateProvider>();
            if (authenticationStateProvider != null)
            {
                var authenticationState = await authenticationStateProvider
                    .GetAuthenticationStateAsync();
                return authenticationState.User;
            }

            return httpContextAccessor.HttpContext?.User
                ?? new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
