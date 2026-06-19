using System;

namespace BlazorFeatures.Abstractions.Options
{
    public interface IFeatureOptions {
        public Type FeatureType { get; }
    }
}
