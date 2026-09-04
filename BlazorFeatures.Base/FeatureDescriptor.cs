using BlazorFeatures.Abstractions.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public sealed class FeatureDescriptor
    {
        internal FeatureDescriptor(
            Type contractType,
            Type requestType,
            Type responseType,
            Type implementationType,
            RenderType renderType,
            ServiceLifetime lifetime,
            bool isActive)
        {
            ContractType = contractType;
            RequestType = requestType;
            ResponseType = responseType;
            ImplementationType = implementationType;
            RenderType = renderType;
            Lifetime = lifetime;
            IsActive = isActive;
        }

        public Type ContractType { get; }

        public Type RequestType { get; }

        public Type ResponseType { get; }

        public Type ImplementationType { get; }

        public Assembly Assembly => ImplementationType.Assembly;

        public RenderType RenderType { get; }

        public ServiceLifetime Lifetime { get; }

        public bool IsOpenGeneric => ImplementationType.IsGenericTypeDefinition;

        public bool IsActive { get; }
    }
}
