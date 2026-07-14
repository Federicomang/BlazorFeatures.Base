using BlazorFeatures.Abstractions.Sse;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#if NET10_0_OR_GREATER
using System.Text;
using System.Net.ServerSentEvents;
#else
using System.Collections.Generic;
using System.IO;
#endif

namespace BlazorFeatures.Abstractions.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, JsonSerializerOptions? options, CancellationToken cancellationToken = default) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, options, cancellationToken);
        }

        public static async Task<FeatureResponse<T>> AsFeatureResponse<T>(this HttpResponseMessage response, Func<string?, Task<FeatureResponse<T>>> customDeserialize, CancellationToken cancellationToken = default) where T : class
        {
            return await FeatureResponse<T>.FromHttpResponse(response, customDeserialize, cancellationToken);
        }

        public static async Task<FeatureResponse<T>> SSE<T>(this HttpClient client, HttpRequestMessage requestMessage, ISseRequest request, SseOptions<T>? options = null, CancellationToken cancellationToken = default) where T : class
        {
            options ??= new();
            var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await options.GenerateFeatureResponse(response, cancellationToken);
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            var isSse = string.Equals(
                mediaType,
                "text/event-stream",
                StringComparison.OrdinalIgnoreCase);

            if (!isSse)
            {
                return await options.GenerateFeatureResponse(response, cancellationToken);
            }

            FeatureResponse<T>? featureRes = null;

#if NET10_0_OR_GREATER
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parser = SseParser.Create(stream, (type, data) =>
            {
                return Encoding.UTF8.GetString(data);
            });

            await foreach (var item in parser.EnumerateAsync(cancellationToken))
            {
                if (item.EventType == options.ResponseEventKeyword)
                {
                    featureRes = await options.GenerateFeatureResponse(response, cancellationToken);
                }

                await request.OnEventSse(item);
            }
#else
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var dataLines = new List<string>();

            string? eventId = null;
            string? eventType = null;
            int? retryMilliseconds = null;
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Riga vuota: dispatch dell'evento.
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (dataLines.Count > 0)
                    {
                        eventType = string.IsNullOrEmpty(eventType) ? "message" : eventType;
                        var data = string.Join("\n", dataLines);

                        if (eventType == options.ResponseEventKeyword)
                        {
                            featureRes = await options.GenerateFeatureResponse(data);
                        }

                        var sseEvent = new SseEvent(eventId!, eventType!, data, retryMilliseconds);
                        await request.OnEventSse(sseEvent);
                    }

                    dataLines.Clear();
                    eventId = null;
                    eventType = null;
                    retryMilliseconds = null;

                    continue;
                }

                // Commento/heartbeat.
                if (line[0] == ':')
                    continue;

                var separatorIndex = line.IndexOf(':');

                string field;
                string value;

                if (separatorIndex < 0)
                {
                    field = line;
                    value = string.Empty;
                }
                else
                {
                    field = line.Substring(0, separatorIndex);
                    value = line.Substring(separatorIndex + 1);

                    // Lo standard rimuove un solo spazio opzionale.
                    if (value.StartsWith(" ", StringComparison.Ordinal))
                        value = value.Substring(1);
                }

                switch (field)
                {
                    case "data":
                        dataLines.Add(value);
                        break;

                    case "event":
                        eventType = value;
                        break;

                    case "id":
                        // Gli ID contenenti NUL devono essere ignorati.
                        if (value.IndexOf('\0') < 0)
                            eventId = value;
                        break;

                    case "retry":
                        if (int.TryParse(value, out int retry) && retry >= 0)
                            retryMilliseconds = retry;
                        break;
                }
            }
#endif
            return featureRes ?? FeatureResponse<T>.AsFailure(messages: [$"The event \"{options.ResponseEventKeyword}\" not arrived, the response cannot be processed"]);
        }
    }
}
