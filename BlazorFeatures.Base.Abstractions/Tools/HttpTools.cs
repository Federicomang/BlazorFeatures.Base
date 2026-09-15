using BlazorFeatures.Abstractions.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions.Tools
{
    public class HttpTools
    {
        public const string TypePrefix = "$type:";

        public static string ToQueryString(
            object obj,
            JsonSerializerOptions? jsonOptions = null)
            => ToQueryStringCore(obj, null, jsonOptions, GetParameterName);

        public static string ToQueryString(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToQueryStringCore(obj, typeHints, jsonOptions, GetParameterName);

        public static MultipartFormDataContent ToMultipartFormDataContent(
            object obj,
            JsonSerializerOptions? jsonOptions = null)
            => ToMultipartFormDataContentCore(
                obj, null, jsonOptions, GetParameterName, AddMultipartValue);

        public static MultipartFormDataContent ToMultipartFormDataContent(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToMultipartFormDataContentCore(
                obj, typeHints, jsonOptions, GetParameterName, AddMultipartValue);

        protected static string ToQueryStringCore(
            object obj,
            IReadOnlyDictionary<string, string>? typeHints,
            JsonSerializerOptions? jsonOptions,
            Func<PropertyInfo, string> getParameterName)
        {
            ThrowIfNull(obj);
            var pairs = new List<string>();

            VisitParameterValues(
                obj,
                getParameterName,
                (name, value) => AddPair(
                    pairs,
                    name,
                    ConvertToInvariantString(value, jsonOptions)));

            AddQueryTypeHints(pairs, typeHints);
            return string.Join("&", pairs);
        }

        protected static MultipartFormDataContent ToMultipartFormDataContentCore(
            object obj,
            IReadOnlyDictionary<string, string>? typeHints,
            JsonSerializerOptions? jsonOptions,
            Func<PropertyInfo, string> getParameterName,
            Action<MultipartFormDataContent, string, object, JsonSerializerOptions?> addValue)
        {
            ThrowIfNull(obj);
            var content = new MultipartFormDataContent();

            try
            {
                VisitParameterValues(
                    obj,
                    getParameterName,
                    (name, value) => addValue(content, name, value, jsonOptions));

                AddMultipartTypeHints(content, typeHints);
                return content;
            }
            catch
            {
                content.Dispose();
                throw;
            }
        }

        protected static void AddMultipartValue(
            MultipartFormDataContent content,
            string name,
            object value,
            JsonSerializerOptions? jsonOptions)
        {
            if (value is MultipartFileData file)
            {
                AddMultipartFile(content, name, file.OpenReadStream, file.FileName, file.Headers);
                return;
            }

            if (value is Stream stream)
            {
                AddMultipartFile(content, name, stream, name, null);
                return;
            }

            AddMultipartText(
                content,
                name,
                ConvertToInvariantString(value, jsonOptions));
        }

        protected static void AddMultipartFile(
            MultipartFormDataContent content,
            string name,
            Stream stream,
            string fileName,
            IReadOnlyDictionary<string, string[]>? headers)
            => AddMultipartFile(content, name, () => stream, fileName, headers);

        protected static void AddMultipartFile(
            MultipartFormDataContent content,
            string name,
            Func<Stream> openReadStream,
            string fileName,
            IReadOnlyDictionary<string, string[]>? headers)
        {
            var streamContent = new DeferredStreamContent(openReadStream);

            try
            {
                if (headers is not null)
                {
                    foreach (var header in headers)
                    {
                        ValidateHeader(header.Key, header.Value);

                        // Name e file name devono provenire dagli argomenti del metodo,
                        // così il binding usa sempre il Content-Disposition standard.
                        if (header.Key.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            if (header.Value.Length != 1 ||
                                !MediaTypeHeaderValue.TryParse(header.Value[0], out var mediaType))
                            {
                                throw new FormatException("L'header Content-Type non è valido.");
                            }

                            streamContent.Headers.ContentType = mediaType;
                            continue;
                        }

                        if (!streamContent.Headers.TryAddWithoutValidation(header.Key, header.Value))
                        {
                            throw new FormatException(
                                $"L'header '{header.Key}' non è valido per un contenuto multipart.");
                        }
                    }
                }

                content.Add(streamContent, name, fileName);
            }
            catch
            {
                streamContent.Dispose();
                throw;
            }
        }

        private static void ValidateHeader(string name, IEnumerable<string> values)
        {
            if (string.IsNullOrWhiteSpace(name) || ContainsNewLine(name))
                throw new FormatException("Il nome di un header multipart non è valido.");

            foreach (var value in values)
            {
                if (value is null || ContainsNewLine(value))
                    throw new FormatException($"Il valore dell'header '{name}' non è valido.");
            }
        }

        private static bool ContainsNewLine(string value)
            => value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;

        private sealed class DeferredStreamContent : HttpContent
        {
            private readonly Func<Stream> _openReadStream;
            private Stream? _stream;

            public DeferredStreamContent(Func<Stream> openReadStream)
            {
                _openReadStream = openReadStream
                    ?? throw new ArgumentNullException(nameof(openReadStream));
            }

            protected override async Task SerializeToStreamAsync(
                Stream stream,
                TransportContext? context)
            {
                _stream ??= _openReadStream()
                    ?? throw new InvalidOperationException(
                        "La funzione di apertura del file ha restituito null.");

                await _stream.CopyToAsync(stream).ConfigureAwait(false);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _stream?.Dispose();

                base.Dispose(disposing);
            }
        }

        protected static bool IsExtensionDataProperty(
            object model,
            PropertyInfo property)
            => property.GetCustomAttribute<JsonExtensionDataAttribute>() is not null ||
               model is IWithUnmanagedData &&
               property.Name == nameof(IWithUnmanagedData.OtherData);

        protected static string ConvertToInvariantString(
            object value,
            JsonSerializerOptions? jsonOptions = null)
        {
            if (value is Stream or MultipartFileData)
            {
                throw new NotSupportedException(
                    "I file e gli stream devono essere inviati con ToMultipartFormDataContent.");
            }

            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.String
                    ? jsonElement.GetString()!
                    : jsonElement.GetRawText();
            }

            if (TryConvertToInvariantString(value, out var converted))
                return converted!;

            return JsonSerializer.Serialize(value, value.GetType(), jsonOptions);
        }

        private static void VisitParameterValues(
            object model,
            Func<PropertyInfo, string> getParameterName,
            Action<string, object> visitor)
        {
            foreach (var property in model.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                var propertyValue = property.GetValue(model);

                if (propertyValue is null)
                    continue;

                if (IsExtensionDataProperty(model, property) &&
                    propertyValue is IDictionary dictionary)
                {
                    foreach (var keyObject in dictionary.Keys)
                    {
                        if (keyObject is null)
                            continue;

                        var value = dictionary[keyObject];

                        if (value is null)
                            continue;

                        var key = Convert.ToString(keyObject, CultureInfo.InvariantCulture)!;
                        VisitValue(key, value, visitor);
                    }

                    continue;
                }

                VisitValue(getParameterName(property), propertyValue, visitor);
            }
        }

        private static void VisitValue(
            string name,
            object value,
            Action<string, object> visitor)
        {
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    return;

                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in jsonElement.EnumerateArray())
                        VisitValue(name, item, visitor);

                    return;
                }
            }

            if (value is Array array)
            {
                foreach (var item in array)
                {
                    if (item is not null)
                        VisitValue(name, item, visitor);
                }

                return;
            }

            visitor(name, value);
        }

        private static string GetParameterName(PropertyInfo property)
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? property.Name;

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }

        private static void AddQueryTypeHints(
            ICollection<string> pairs,
            IReadOnlyDictionary<string, string>? typeHints)
        {
            VisitTypeHints(
                typeHints,
                (name, type) => AddPair(pairs, TypePrefix + name, type));
        }

        private static void AddMultipartTypeHints(
            MultipartFormDataContent content,
            IReadOnlyDictionary<string, string>? typeHints)
        {
            VisitTypeHints(
                typeHints,
                (name, type) => AddMultipartText(content, TypePrefix + name, type));
        }

        private static void AddMultipartText(
            MultipartFormDataContent content,
            string name,
            string value)
        {
            var stringContent = new StringContent(value, Encoding.UTF8);

            try
            {
                content.Add(stringContent, name);
            }
            catch
            {
                stringContent.Dispose();
                throw;
            }
        }

        private static void VisitTypeHints(
            IReadOnlyDictionary<string, string>? typeHints,
            Action<string, string> visitor)
        {
            if (typeHints is null)
                return;

            foreach (var hint in typeHints)
            {
                if (string.IsNullOrWhiteSpace(hint.Key))
                    throw new ArgumentException("Il nome del parametro non può essere vuoto.", nameof(typeHints));

                if (string.IsNullOrWhiteSpace(hint.Value))
                {
                    throw new ArgumentException(
                        $"Il tipo del parametro '{hint.Key}' non può essere vuoto.",
                        nameof(typeHints));
                }

                visitor(hint.Key, hint.Value.Trim());
            }
        }

        private static void AddPair(
            ICollection<string> pairs,
            string name,
            string value)
        {
            pairs.Add($"{WebUtility.UrlEncode(name)}={WebUtility.UrlEncode(value)}");
        }

        private static bool TryConvertToInvariantString(
            object value,
            out string? result)
        {
            if (value is string str)
            {
                result = str;
                return true;
            }

            if (value.GetType().IsEnum)
            {
                result = value.ToString();
                return true;
            }

            var converter = TypeDescriptor.GetConverter(value.GetType());

            if (converter.CanConvertFrom(typeof(string)) &&
                converter.CanConvertTo(typeof(string)))
            {
                result = converter.ConvertToInvariantString(value);

                if (result is not null)
                    return true;
            }

            result = null;
            return false;
        }

        private static void ThrowIfNull(object obj)
        {
#if NET10_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(obj);
#else
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
#endif
        }
    }
}
