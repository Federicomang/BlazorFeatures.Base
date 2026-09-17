using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    /// <summary>
    /// Captures data from the caller scope before the feature scope is created.
    /// </summary>
    public interface IFeatureCallerContextEnricher
    {
        ValueTask EnrichAsync(
            FeatureCallerContextBuilder context,
            CancellationToken cancellationToken = default);
    }
}
