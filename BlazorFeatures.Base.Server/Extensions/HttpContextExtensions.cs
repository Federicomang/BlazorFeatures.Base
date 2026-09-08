using BlazorFeatures.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace BlazorFeatures.Base.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task RunFeature<Response>(this HttpContext context, IFeatureService featureService, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            _ = await RunFeatureAndGetResult(context, featureService, request, cancellationToken);
        }

        public static async Task RunFeature<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            _ = await RunFeatureAndGetResult(context, request, cancellationToken);
        }

        public static async Task<FeatureResponse<Response>> RunFeatureAndGetResult<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            var featureService = context.RequestServices.GetRequiredService<IFeatureService>();
            return await RunFeatureAndGetResult(context, featureService, request, cancellationToken);
        }

        public static async Task<FeatureResponse<Response>> RunFeatureAndGetResult<Response>(this HttpContext context, IFeatureService featureService, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            if (cancellationToken == default)
            {
                cancellationToken = context.RequestAborted;
            }

            var featureContext = new HttpFeatureContext(context);
            var response = await featureService.Run(request, featureContext, cancellationToken);
            var jsonOptions = context.RequestServices.GetService<IOptions<JsonSerializerOptions>>()?.Value;
            await featureContext.ApplyApiFeatureResponse(request, response, jsonOptions);
            return response;
        }

        public static async Task ApplyApiFeatureResponse<Response>(this IHttpFeatureContext featureContext, IBaseFeatureRequest<Response> request, FeatureResponse<Response> response, JsonSerializerOptions? jsonOptions) where Response : class
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
                result = Results.Json(response, options: jsonOptions, statusCode: (int)statusCode);
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
