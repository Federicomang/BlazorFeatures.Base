using System.Text.Json;
using System.Threading.Tasks;
#if NET10_0_OR_GREATER
using System.Net.ServerSentEvents;
#endif

namespace BlazorFeatures.Abstractions.Sse
{
    public interface ISseRequest
    {
#if NET10_0_OR_GREATER
        public Task OnEventSse(SseItem<string> value, JsonSerializerOptions? jsonSerializerOptions);
#else
        public Task OnEventSse(SseEvent value, JsonSerializerOptions? jsonSerializerOptions);
#endif
    }
}
