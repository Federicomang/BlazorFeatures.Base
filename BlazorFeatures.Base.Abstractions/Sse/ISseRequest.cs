using System.Threading.Tasks;
#if NET10_0_OR_GREATER
using System.Net.ServerSentEvents;
#endif

namespace BlazorFeatures.Abstractions.Sse
{
    public interface ISseRequest
    {
#if NET10_0_OR_GREATER
        public Task OnEventSse(SseItem<string> value);
#else
        public Task OnEventSse(SseEvent value);
#endif
    }
}
