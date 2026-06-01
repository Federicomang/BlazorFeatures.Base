using Microsoft.JSInterop;

namespace BlazorFeatures.Abstractions.Server.Extensions
{
    public static class JSRuntimeExtensions
    {
        public static async Task<BrowserDoRequest<string>> DoRequest(this IJSRuntime jsRuntime, string url, object requestData)
        {
            return await jsRuntime.InvokeAsync<BrowserDoRequest<string>>("window.doRequest", url, requestData, false);
        }

        public static async Task<BrowserDoRequest<T>> DoRequestAsJson<T>(this IJSRuntime jsRuntime, string url, object requestData) where T : class
        {
            return await jsRuntime.InvokeAsync<BrowserDoRequest<T>>("window.doRequest", url, requestData, true);
        }
    }
}
