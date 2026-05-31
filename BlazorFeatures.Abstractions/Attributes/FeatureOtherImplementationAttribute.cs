namespace BlazorFeatures.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class FeatureOtherImplementationAttribute(params Type[] types) : Attribute
    {
        public IEnumerable<Type> Types { get; init; } = types;
    }
}
