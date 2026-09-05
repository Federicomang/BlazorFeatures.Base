using BlazorFeatures.Abstractions;
using System.Security.Claims;

namespace BlazorFeatures.Base.Server
{
    /// <summary>
    /// Resolves the principal used by the mandatory server-side feature authorization.
    /// Applications can replace the default implementation when authentication state
    /// requires host-specific refresh logic.
    /// </summary>
    public interface IFeaturePrincipalProvider
    {
        ValueTask<ClaimsPrincipal> GetPrincipalAsync(
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default);
    }
}
