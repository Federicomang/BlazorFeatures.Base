#if NET10_0_OR_GREATER
#else
namespace BlazorFeatures.Abstractions.Sse
{
    public sealed class SseEvent(string? id, string eventType, string data, int? retryMilliseconds = null)
    {
        public string? Id => id;
        public string EventType => eventType;
        public string Data => data;
        public int? RetryMilliseconds => retryMilliseconds;
    }
}
#endif
