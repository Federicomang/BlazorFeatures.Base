namespace BlazorFeatures.Abstractions.Enums
{
    public enum FeatureInvocationSource
    {
        Unknown,
        Client,
        Server,
        Http,
        Internal,
        BackgroundJob
    }
}
