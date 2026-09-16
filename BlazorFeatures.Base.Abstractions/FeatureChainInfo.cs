using System;

namespace BlazorFeatures.Abstractions
{
    public sealed class FeatureChainInfo
    {
        public Guid NodeId { get; }

        public Guid? ParentNodeId { get; }

        public IBaseFeatureRequest Request { get; }

        public IServiceProvider ServiceProvider { get; }

        internal FeatureChainInfo(
            Guid? parentNodeId,
            IFeatureContext context,
            IServiceProvider serviceProvider)
        {
            ParentNodeId = parentNodeId;
            NodeId = context.NodeId;
            Request = context.FeatureRequest;
            ServiceProvider = serviceProvider;
        }
    }
}
