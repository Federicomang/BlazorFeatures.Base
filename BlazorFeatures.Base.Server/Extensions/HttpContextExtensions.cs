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
        public class RunFeatureConfig
        {
            public IFeatureService? FeatureService { get; set; }

            public JsonSerializerOptions? JsonSerializerOptions { get; set; }

            public IHttpFeatureContext FeatureContext { get; private set; }

            internal RunFeatureConfig(HttpFeatureContext featureContext)
            {
                FeatureContext = featureContext;
                JsonSerializerOptions = featureContext.HttpContext.RequestServices.GetService<IOptions<JsonSerializerOptions>>()?.Value;
            }
        }

        public static async Task RunFeature<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, Action<RunFeatureConfig>? configBuilder, CancellationToken cancellationToken = default) where Response : class
        {
            _ = await RunFeatureAndGetResult(context, request, configBuilder, cancellationToken);
        }

        public static async Task RunFeature<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            _ = await RunFeatureAndGetResult(context, request, cancellationToken);
        }

        public static async Task<FeatureResponse<Response>> RunFeatureAndGetResult<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            return await RunFeatureAndGetResult(context, request, null, cancellationToken);
        }

        public static async Task<FeatureResponse<Response>> RunFeatureAndGetResult<Response>(this HttpContext context, IBaseFeatureRequest<Response> request, Action<RunFeatureConfig>? configBuilder, CancellationToken cancellationToken = default) where Response : class
        {
            if (cancellationToken == default)
            {
                cancellationToken = context.RequestAborted;
            }

            var featureContext = new HttpFeatureContext(context, request);
            var config = new RunFeatureConfig(featureContext);
            configBuilder?.Invoke(config);
            var featureService = config.FeatureService ?? context.RequestServices.GetRequiredService<IFeatureService>();
            var response = await featureService.Run(request, featureContext, cancellationToken);
            await featureContext.ApplyApiFeatureResponse(request, response, config.JsonSerializerOptions);
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
