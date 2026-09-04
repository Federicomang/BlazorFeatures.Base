using BlazorFeatures.Abstractions;

using BlazorFeatures.Abstractions.Enums;

namespace BlazorFeatures.Base
{
    public class BaseFeatureContext : IFeatureContext
    {
        public Guid OperationId { get; init; }

        public FeatureInvocationSource InvocationSource { get; init; }

        public List<IBaseFeatureRequest> FeatureChain { get; init; } = [];

        public Dictionary<string, object> Values { get; init; } = [];

        public BaseFeatureContext(FeatureInvocationSource invocationSource = FeatureInvocationSource.Unknown, Guid? operationId = null)
        {
            InvocationSource = invocationSource;
            OperationId = operationId ?? Guid.NewGuid();
        }
    }
}
