using BlazorFeatures.Abstractions.Extensions;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions.Sse
{
    public class SseOptions<T> where T : class
    {
        public Func<string?, Task<FeatureResponse<T>>>? ResponseCustomDeserialize { get; set; }

        public string ResponseEventKeyword { get; set; } = "feature_response";

        public JsonSerializerOptions? JsonSerializerOptions { get; set; }

        public async Task<FeatureResponse<T>> GenerateFeatureResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (ResponseCustomDeserialize == null)
            {
                return await response.AsFeatureResponse<T>(JsonSerializerOptions, cancellationToken);
            }
            else
            {
                return await response.AsFeatureResponse(ResponseCustomDeserialize, cancellationToken);
            }
        }

        public async Task<FeatureResponse<T>> GenerateFeatureResponse(string data)
        {
            if (ResponseCustomDeserialize == null)
            {
                return JsonSerializer.Deserialize<FeatureResponse<T>>(data, JsonSerializerOptions)!;
            }
            else
            {
                return await ResponseCustomDeserialize(data);
            }
        }
    }
}
