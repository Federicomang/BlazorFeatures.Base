using BlazorFeatures.Abstractions.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureContext
    {
        public Guid NodeId { get; }

        public Guid OperationId { get; }

        public FeatureInvocationSource InvocationSource { get; }

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
        /// Creates the context scope used for the next direct feature invocation.
        /// </summary>
        public IFeatureContext CreateInvocationScope(IBaseFeatureRequest featureRequest);
    }
}
