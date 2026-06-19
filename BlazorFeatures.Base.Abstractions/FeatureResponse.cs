using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    public class FeatureResponse<T> where T : class
    {
        [JsonInclude]
        [JsonPropertyName("success")]
        private bool? _success;

        [JsonInclude]
        public T? Data { get; private set; }

        [JsonInclude]
        public List<string> Messages { get; private set; } = [];

        [JsonInclude]
        public IDictionary<string, string[]>? ValidationErrors { get; set; }

        [JsonIgnore]
        public HttpResponseMessage? HttpResponseMessage { get; private set; }

        [JsonIgnore]
        public bool Success
        {
            get => _success ?? HttpResponseMessage?.IsSuccessStatusCode ?? false;
            private set => _success = value;
        }

        public FeatureResponse<object> AsGeneric()
        {
            return FeatureResponse<object>.Create(Success, Data, Messages, ValidationErrors);
        }

        public FeatureResponse<K> ConvertTo<K>() where K : class, T
        {
            return FeatureResponse<K>.Create(Success, (K?)Data, Messages, ValidationErrors);
        }

        public FeatureResponse<K> ConvertTo<K>(K onSuccessData, K? onFailureData = null) where K : class
        {
            var data = Success ? onSuccessData : onFailureData;
            return FeatureResponse<K>.Create(Success, data, Messages, ValidationErrors);
        }

        public FeatureResponse<K> ConvertTo<K>(Func<FeatureResponse<T>, K?> builder) where K : class
        {
            return FeatureResponse<K>.Create(Success, builder(this), Messages, ValidationErrors);
        }

        public static async Task<FeatureResponse<T>> FromHttpResponse(HttpResponseMessage response, JsonSerializerOptions? options)
        {
            FeatureResponse<T> featureRes;
            var dataStr = await response.Content.ReadAsStringAsync();
            try
            {
                featureRes = JsonSerializer.Deserialize<FeatureResponse<T>>(dataStr, options)!;
            }
            catch (Exception)
            {
                featureRes = Create();
            }
            featureRes.HttpResponseMessage = response;
            return featureRes;
        }

        public static async Task<FeatureResponse<T>> FromHttpResponse(HttpResponseMessage response, Func<string?, Task<FeatureResponse<T>>> customDeserialize)
        {
            FeatureResponse<T> featureRes;
            var dataStr = await response.Content.ReadAsStringAsync();
            try
            {
                featureRes = await customDeserialize(dataStr);
            }
            catch (Exception)
            {
                featureRes = Create();
            }
            featureRes.HttpResponseMessage = response;
            return featureRes;
        }

        public static FeatureResponse<T> Create(bool success = false, T? data = null, IEnumerable<string>? messages = null, IDictionary<string, string[]>? validationErrors = null)
        {
            return new FeatureResponse<T>()
            {
                Success = success,
                Data = data,
                Messages = messages?.ToList() ?? [],
                ValidationErrors = validationErrors
            };
        }

        public static FeatureResponse<T> AsSuccess(T? data) => Create(true, data);

        public static FeatureResponse<T> AsFailure(T? data = null, IEnumerable<string>? messages = null, IDictionary<string, string[]>? validationErrors = null) => Create(false, data, messages, validationErrors);
                
    }
}
