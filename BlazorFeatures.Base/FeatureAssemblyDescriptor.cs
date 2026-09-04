using BlazorFeatures.Abstractions.Enums;
using System.Collections.ObjectModel;
using System.Reflection;

namespace BlazorFeatures.Base
{
    public sealed class FeatureAssemblyDescriptor
    {
        private readonly ReadOnlyCollection<Type> _types;

        internal FeatureAssemblyDescriptor(
            Assembly assembly,
            RenderType renderType,
            bool isExplicitlyRegistered,
            Type[] types)
        {
            Assembly = assembly;
            RenderType = renderType;
            IsExplicitlyRegistered = isExplicitlyRegistered;
            _types = Array.AsReadOnly(types);
        }

        public Assembly Assembly { get; }

        public RenderType RenderType { get; }

        public bool IsExplicitlyRegistered { get; }

        public IReadOnlyList<Type> Types => _types;
    }
}
