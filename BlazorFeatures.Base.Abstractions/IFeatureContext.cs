using BlazorFeatures.Abstractions.Enums;
using System;
using System.Collections.Generic;

namespace BlazorFeatures.Abstractions
{
    public interface IFeatureContext
    {
        public Guid OperationId { get; }

        public FeatureInvocationSource InvocationSource { get; }

        public List<IBaseFeatureRequest> FeatureChain { get; }

        public Dictionary<string, object> Values { get; }
    }
}
