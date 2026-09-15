#if NET10_0_OR_GREATER
using System.Net.ServerSentEvents;
#endif

namespace BlazorFeatures.Abstractions.Sse
{
    public sealed class SseEvent(string? id, string eventType, string data, int? retryMilliseconds = null)
    {
#if NET10_0_OR_GREATER
        public SseItem<string>? OriginalItem { get; init; }

        public SseEvent(SseItem<string> item) : this(item.EventId, item.EventType, item.Data, item.ReconnectionInterval.HasValue ? item.ReconnectionInterval.Value.Milliseconds : null)
        {
            OriginalItem = item;
        }
#endif
        public string? Id => id;
        public string EventType => eventType;
        public string Data => data;
        public int? RetryMilliseconds => retryMilliseconds;
    }
}
