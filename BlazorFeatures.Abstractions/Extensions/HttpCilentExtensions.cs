using System.Text.Json;

namespace BlazorFeatures.Abstractions.Extensions
{
    public static class HttpCilentExtensions
    {
        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, options);
        }

        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, Func<string?, Task<FeatureResponse<T>>> customDeserialize) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, customDeserialize);
        }
    }
}
