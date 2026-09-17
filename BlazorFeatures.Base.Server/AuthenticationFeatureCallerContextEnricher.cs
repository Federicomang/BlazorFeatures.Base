using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Server
{
    internal sealed class AuthenticationFeatureCallerContextEnricher(
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor) : IFeatureCallerContextEnricher
    {
        public async ValueTask EnrichAsync(
            FeatureCallerContextBuilder context,
            CancellationToken cancellationToken = default)
        {
            if (httpContextAccessor.HttpContext is { } httpContext)
            {
                context.User = httpContext.User;
                return;
            }

            var authenticationStateProvider =
                serviceProvider.GetService<AuthenticationStateProvider>();
            if (authenticationStateProvider == null)
                return;

            var authenticationState = await authenticationStateProvider
                .GetAuthenticationStateAsync();
            context.User = authenticationState.User;
        }
    }
}
