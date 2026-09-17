using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base
{
    internal sealed class AuthenticationFeatureCallerContextEnricher(
        IServiceProvider serviceProvider) : IFeatureCallerContextEnricher
    {
        public async ValueTask EnrichAsync(
            FeatureCallerContextBuilder context,
            CancellationToken cancellationToken = default)
        {
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
