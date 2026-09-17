using BlazorFeatures.Abstractions.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureContext
    {
        public Guid NodeId { get; }

        public Guid OperationId { get; }

        public FeatureInvocationSource InvocationSource { get; }

        public FeatureCallerContext CallerContext { get; }

        public IBaseFeatureRequest FeatureRequest { get; }

        public ConcurrentDictionary<Guid, FeatureChainInfo> FeatureChain { get; }

        /// <summary>
        /// Gets the values visible to the current feature. Temporary values take
        /// precedence over permanent values when the same key exists in both.
        /// </summary>
        public IReadOnlyDictionary<string, object> Values { get; }

        /// <summary>
        /// Gets the values that are valid for the current feature invocation.
        /// Values written by the current feature are passed to every direct
        /// feature invocation created from this context.
        /// </summary>
        public IDictionary<string, object> TempValues { get; }

        /// <summary>
        /// Gets the values shared by every feature in this context chain.
        /// </summary>
        public IDictionary<string, object> PermanentValues { get; }

        /// <summary>
        /// Gets the lifetime shared by all invocations that use the same
        /// feature service scope.
        /// </summary>
        public FeatureScopeLifetime ScopeLifetime { get; }

        /// <summary>
        /// When enabled, direct child invocations reuse this feature's service
        /// scope. It is disabled again on each child context.
        /// </summary>
        /// <remarks>
        /// Do not enable this for parallel child invocations unless every
        /// scoped service involved is safe for concurrent use.
        /// </remarks>
        public bool UseSameServiceScope { get; set; }

        /// <summary>
        /// Keeps the service scope alive until the supplied operation completes.
        /// </summary>
        public void DeferScopeDisposalUntil(Task operation);

        /// <summary>
        /// Creates the context scope used for the next direct feature invocation.
        /// </summary>
        public IFeatureContext CreateInvocationScope(
            IBaseFeatureRequest featureRequest,
            FeatureCallerContext? callerContext = null,
            FeatureScopeLifetime? scopeLifetime = null);
    }
}
