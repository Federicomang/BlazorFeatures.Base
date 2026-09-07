using Microsoft.AspNetCore.Http;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class HttpFeatureContextExtensions
    {
        public static void SetHttpResult(this IHttpFeatureContext context, IResult result)
        {
            context.SetHttpResult(context.FeatureChain.Last(), result);
        }
    }
}
