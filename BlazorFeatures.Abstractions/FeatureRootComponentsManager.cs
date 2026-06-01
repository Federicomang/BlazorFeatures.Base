namespace BlazorFeatures.Abstractions
{
    public class FeatureRootComponentsManager
    {
        public IReadOnlyList<Type> RootComponents { get; init; }

        internal FeatureRootComponentsManager(List<Type> rootComponents)
        {
            RootComponents = rootComponents;
        }
    }
}
