namespace BlazorFeatures.Base
{
    public sealed class FeatureResolution
    {
        internal FeatureResolution(FeatureDescriptor descriptor, Type implementationType)
        {
            Descriptor = descriptor;
            ImplementationType = implementationType;
        }

        public FeatureDescriptor Descriptor { get; }

        public Type ImplementationType { get; }

        public bool WasClosedFromGeneric => Descriptor.IsOpenGeneric;
    }
}
