namespace BlazorFeatures.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FeaturePolicyNameAttribute(string name) : Attribute
    {
        public string Name => name;
    }
}
