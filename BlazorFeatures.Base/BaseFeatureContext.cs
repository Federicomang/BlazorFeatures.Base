using BlazorFeatures.Abstractions;

using BlazorFeatures.Abstractions.Enums;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace BlazorFeatures.Base
{
    [SuppressMessage("Style", "IDE0028:Semplifica l'inizializzazione della raccolta")]
    public class BaseFeatureContext : IFeatureContext
    {
        private readonly ScopedTempValues _tempValues;
        public Guid NodeId { get; init; }

        public Guid OperationId { get; init; }

        public FeatureInvocationSource InvocationSource { get; init; }

        public FeatureCallerContext CallerContext { get; init; }

        public IBaseFeatureRequest FeatureRequest { get; init; }

        public ConcurrentDictionary<Guid, FeatureChainInfo> FeatureChain { get; init; } = [];

        public IReadOnlyDictionary<string, object> Values { get; }

        public IDictionary<string, object> TempValues => _tempValues;

        public IDictionary<string, object> PermanentValues { get; }

        public FeatureScopeLifetime ScopeLifetime { get; init; }

        public bool UseSameServiceScope { get; set; } = false;

        public BaseFeatureContext(
            IBaseFeatureRequest featureRequest,
            FeatureInvocationSource invocationSource = FeatureInvocationSource.Unknown,
            Guid? operationId = null,
            FeatureCallerContext? callerContext = null)
        {
            NodeId = Guid.NewGuid();
            FeatureRequest = featureRequest;
            InvocationSource = invocationSource;
            OperationId = operationId ?? Guid.NewGuid();
            CallerContext = callerContext ?? new FeatureCallerContext(
                new ClaimsPrincipal(new ClaimsIdentity()));
            ScopeLifetime = new FeatureScopeLifetime();
            PermanentValues = new ConcurrentDictionary<string, object>();
            _tempValues = new ScopedTempValues();
            Values = new FeatureContextValues(PermanentValues, _tempValues);
        }

        protected BaseFeatureContext(
            IBaseFeatureRequest featureRequest,
            BaseFeatureContext parent,
            FeatureCallerContext? callerContext = null,
            FeatureScopeLifetime? scopeLifetime = null)
        {
            ArgumentNullException.ThrowIfNull(parent);

            NodeId = Guid.NewGuid();
            FeatureRequest = featureRequest;
            InvocationSource = parent.InvocationSource;
            OperationId = parent.OperationId;
            CallerContext = callerContext ?? parent.CallerContext;
            ScopeLifetime = scopeLifetime ?? new FeatureScopeLifetime();
            FeatureChain = parent.FeatureChain;
            PermanentValues = parent.PermanentValues;
            _tempValues = new(parent._tempValues.GetOutgoingValuesSnapshot());
            Values = new FeatureContextValues(PermanentValues, _tempValues);
        }

        public virtual IFeatureContext CreateInvocationScope(
            IBaseFeatureRequest request,
            FeatureCallerContext? callerContext = null,
            FeatureScopeLifetime? scopeLifetime = null) =>
            new BaseFeatureContext(request, this, callerContext, scopeLifetime);

        public void DeferScopeDisposalUntil(Task operation) =>
            ScopeLifetime.DeferDisposalUntil(operation);
    }
}
