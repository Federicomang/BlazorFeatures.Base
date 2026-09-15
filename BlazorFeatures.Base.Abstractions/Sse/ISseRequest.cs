using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions.Sse
{
    public interface ISseRequest
    {
        public Task OnEventSse(SseEvent value, JsonSerializerOptions? jsonSerializerOptions);
    }
}
