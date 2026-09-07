using BlazorFeatures.Abstractions;

using BlazorFeatures.Abstractions.Enums;
using System.Diagnostics.CodeAnalysis;

namespace BlazorFeatures.Base
{
    [SuppressMessage("Style", "IDE0028:Semplifica l'inizializzazione della raccolta")]
    public class BaseFeatureContext : IFeatureContext
    {
        private readonly ScopedTempValues _tempValues;

        public Guid OperationId { get; init; }

        public FeatureInvocationSource InvocationSource { get; init; }

        public List<IBaseFeatureRequest> FeatureChain { get; init; } = [];

        public IReadOnlyDictionary<string, object> Values { get; }

        public IDictionary<string, object> TempValues => _tempValues;

        public IDictionary<string, object> PermanentValues { get; }

        public BaseFeatureContext(FeatureInvocationSource invocationSource = FeatureInvocationSource.Unknown, Guid? operationId = null)
        {
            InvocationSource = invocationSource;
            OperationId = operationId ?? Guid.NewGuid();
            PermanentValues = new Dictionary<string, object>();
            _tempValues = new ScopedTempValues();
            Values = new FeatureContextValues(PermanentValues, _tempValues);
        }

        protected BaseFeatureContext(BaseFeatureContext parent)
        {
            ArgumentNullException.ThrowIfNull(parent);

            InvocationSource = parent.InvocationSource;
            OperationId = parent.OperationId;
            FeatureChain = parent.FeatureChain;
            PermanentValues = parent.PermanentValues;
            _tempValues = new(parent._tempValues.TakeOutgoingValues());
            Values = new FeatureContextValues(PermanentValues, _tempValues);
        }

        public virtual IFeatureContext CreateInvocationScope() =>
            new BaseFeatureContext(this);
    }
}
