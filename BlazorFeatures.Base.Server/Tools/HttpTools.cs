using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Base.Server.Tools
{
    public class HttpTools : Abstractions.Tools.HttpTools
    {
        public new static string ToUrlEncodedString(object obj, JsonSerializerOptions? jsonOptions = null)
            => ToUrlEncodedStringCore(obj, null, jsonOptions);

        public new static string ToUrlEncodedString(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToUrlEncodedStringCore(obj, typeHints, jsonOptions);

        private static string ToUrlEncodedStringCore(
            object obj,
            IReadOnlyDictionary<string, string>? typeHints,
            JsonSerializerOptions? jsonOptions)
        {
            ArgumentNullException.ThrowIfNull(obj);

            var pairs = new List<string>();

            foreach (var property in obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead)
                    continue;

                var propertyValue = property.GetValue(obj);

                if (propertyValue is null)
                    continue;

                if (IsExtensionDataProperty(obj, property) &&
                    propertyValue is IDictionary dictionary)
                {
                    foreach (var keyObject in dictionary.Keys)
                    {
                        if (keyObject is null)
                            continue;

                        var value = dictionary[keyObject];

                        if (value is null)
                            continue;

                        var key = Convert.ToString(
                            keyObject,
                            CultureInfo.InvariantCulture)!;

                        AddValue(pairs, key, value, jsonOptions);
                    }

                    continue;
                }

                var name = GetQueryParameterName(property);

                AddValue(pairs, name, propertyValue, jsonOptions);
            }

            AddTypeHints(pairs, typeHints);

            return string.Join("&", pairs);
        }

        private static string GetQueryParameterName(
            PropertyInfo property)
        {
            var name = property.GetCustomAttribute<FromQueryAttribute>()?.Name ??
                property.GetCustomAttribute<FromFormAttribute>()?.Name ??
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
