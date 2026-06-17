using Microsoft.AspNetCore.Http;

namespace BlazorFeatures.Abstractions.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static void SetFeatureApiResponse(this HttpContext context, IResult resultResponse)
        {
            if (!context.IsSocketConnection())
            {
                context.Items["ApiResult"] = resultResponse;
            }
        }

        public static async Task ApplyApiFeatureResponse(this HttpContext context)
        {
            if (context.Items.TryGetValue("ApiResult", out var apiResult))
            {
                await ((IResult)apiResult!).ExecuteAsync(context);
            }
        }

        public static IResult GetApiFeatureResponse(this HttpContext context)
        {
            if (context.Items.TryGetValue("ApiResult", out var apiResult))
            {
                return (IResult)apiResult!;
            }
            return Results.Empty;
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
