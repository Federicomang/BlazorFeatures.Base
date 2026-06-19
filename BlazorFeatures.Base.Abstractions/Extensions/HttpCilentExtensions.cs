using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions.Extensions
{
    public static class HttpCilentExtensions
    {
        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, JsonSerializerOptions? options) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, options);
        }

        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, Func<string?, Task<FeatureResponse<T>>> customDeserialize) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, customDeserialize);
        }
    }
}
