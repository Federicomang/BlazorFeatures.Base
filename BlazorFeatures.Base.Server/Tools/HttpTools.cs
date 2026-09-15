using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Base.Server.Tools
{
    public class HttpTools : Abstractions.Tools.HttpTools
    {
        public new static string ToQueryString(
            object obj,
            JsonSerializerOptions? jsonOptions = null)
            => ToQueryStringCore(obj, null, jsonOptions, GetQueryParameterName);

        public new static string ToQueryString(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToQueryStringCore(obj, typeHints, jsonOptions, GetQueryParameterName);

        public new static MultipartFormDataContent ToMultipartFormDataContent(
            object obj,
            JsonSerializerOptions? jsonOptions = null)
            => ToMultipartFormDataContentCore(
                obj, null, jsonOptions, GetFormParameterName, AddServerMultipartValue);

        public new static MultipartFormDataContent ToMultipartFormDataContent(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToMultipartFormDataContentCore(
                obj, typeHints, jsonOptions, GetFormParameterName, AddServerMultipartValue);

        private static void AddServerMultipartValue(
            MultipartFormDataContent content,
            string name,
            object value,
            JsonSerializerOptions? jsonOptions)
        {
            if (value is IFormFile file)
            {
                AddMultipartFile(
                    content,
                    name,
                    file.OpenReadStream,
                    file.FileName,
                    CopyHeaders(file.Headers));
                return;
            }

            AddMultipartValue(content, name, value, jsonOptions);
        }

        private static IReadOnlyDictionary<string, string[]> CopyHeaders(
            IHeaderDictionary headers)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
                result[header.Key] = header.Value.Select(value => value ?? string.Empty).ToArray();

            return result;
        }

        private static string GetQueryParameterName(PropertyInfo property)
        {
            var name = property.GetCustomAttribute<FromQueryAttribute>()?.Name ??
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                property.Name;

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }

        private static string GetFormParameterName(PropertyInfo property)
        {
            var name = property.GetCustomAttribute<FromFormAttribute>()?.Name ??
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                property.Name;

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }

        public static string BuildServerApi(string s, [StringSyntax("Route")] string route)
        {
            return string.Format(s, args: route.Split(';'));
        }
    }
}
