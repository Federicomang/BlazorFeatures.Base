using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlazorFeatures.Abstractions.Tools
{
    public sealed class MultipartFileData
    {
        private const string ContentTypeHeaderName = "Content-Type";
        private readonly Lazy<Stream> _content;

        public Stream Content => _content.Value;
        public string FileName { get; }
        public IReadOnlyDictionary<string, string[]> Headers { get; }

        public string? ContentType
            => Headers.TryGetValue(ContentTypeHeaderName, out var values)
                ? values.FirstOrDefault()
                : null;

        public Stream OpenReadStream() => Content;

        public MultipartFileData(
            Stream content,
            string fileName,
            string? contentType = null,
            IReadOnlyDictionary<string, string[]>? headers = null)
            : this(CreateStreamFactory(content), fileName, contentType, headers)
        {
        }

        public MultipartFileData(
            Func<Stream> openReadStream,
            string fileName,
            string? contentType = null,
            IReadOnlyDictionary<string, string[]>? headers = null)
        {
#if NET10_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(openReadStream, nameof(openReadStream));
#else
            if (openReadStream is null)
                throw new ArgumentNullException(nameof(openReadStream));
#endif
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Il nome del file non può essere vuoto.", nameof(fileName));

            _content = new Lazy<Stream>(() =>
                openReadStream()
                ?? throw new InvalidOperationException("La funzione di apertura del file ha restituito null."));
            FileName = fileName;

            var copiedHeaders = new Dictionary<string, string[]>(
                StringComparer.OrdinalIgnoreCase);

            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key))
                        throw new ArgumentException("Il nome di un header non può essere vuoto.", nameof(headers));

                    if (header.Value is null)
                        throw new ArgumentException($"L'header '{header.Key}' non può avere valori null.", nameof(headers));

                    copiedHeaders[header.Key] = header.Value.ToArray();
                }
            }

            if (!string.IsNullOrWhiteSpace(contentType))
                copiedHeaders[ContentTypeHeaderName] = [contentType!];

            Headers = copiedHeaders;
        }

        private static Func<Stream> CreateStreamFactory(Stream content)
        {
#if NET10_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(content, nameof(content));
#else
            if (content is null)
                throw new ArgumentNullException(nameof(content));
#endif
            return () => content;
        }
    }
}
