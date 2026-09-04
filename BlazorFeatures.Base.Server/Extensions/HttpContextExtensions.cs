using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<FeatureResponse<Response>> RunFeature<Response>(this HttpContext context, IFeatureService featureService, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            if (cancellationToken == default)
            {
                cancellationToken = context.RequestAborted;
            }

            var featureContext = new HttpFeatureContext(context);
            var response = await featureService.Run(request, featureContext, cancellationToken);
            await featureContext.ApplyApiFeatureResponse(request, response);
            return response;
        }

        public static async Task ApplyApiFeatureResponse<Response>(this IHttpFeatureContext featureContext, IBaseFeatureRequest<Response> request, FeatureResponse<Response> response) where Response : class
        {
            IResult result;
            if (featureContext.TryGetHttpResult(request, out var customResult))
            {
                result = customResult!;
            }
            else
            {
                var statusCode = response.StatusCode
                    ?? (response.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
                result = Results.Json(response, statusCode: (int)statusCode);
            }

            await result.ExecuteAsync(featureContext.HttpContext);
        }

        public static bool IsBrowserRequest(this HttpContext context)
        {
            return context.Request.GetTypedHeaders().Accept.Any(h =>
                h.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            ) == true;
        }

        public static bool IsSocketConnection(this HttpContext context)
        {
            return context.Request.Method == "CONNECT" || context.WebSockets.IsWebSocketRequest;
        }
    }
}
