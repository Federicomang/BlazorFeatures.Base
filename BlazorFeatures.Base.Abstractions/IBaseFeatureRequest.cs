namespace BlazorFeatures.Abstractions
{
    public interface IBaseFeatureRequest
    {
    }

    public interface IBaseFeatureRequest<Response> : IBaseFeatureRequest where Response : class
    {
    }
}
