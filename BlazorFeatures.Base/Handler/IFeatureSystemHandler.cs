using System.Reflection;

namespace BlazorFeatures.Base.Handler
{
    internal interface IFeatureSystemHandler
    {
        void HandlePolicies(List<(string Name, MethodInfo Builder)> policies);
    }

    internal static class FeatureSystemHandlerConstants
    {
        public const string Client = "A8FCBAF5-B55D-4BDA-92F5-35CF4835E79C";

        public const string Server = "D47495B5-3F61-4D19-A00E-505E451351C7";
    }
}
