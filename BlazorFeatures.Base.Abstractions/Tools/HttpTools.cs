using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorFeatures.Abstractions.Interfaces;

namespace BlazorFeatures.Abstractions.Tools
{
    public class HttpTools
    {
        public const string TypePrefix = "$type:";

        public static string ToUrlEncodedString(object obj, JsonSerializerOptions? jsonOptions = null)
            => ToUrlEncodedStringCore(obj, null, jsonOptions);

        public static string ToUrlEncodedString(
            object obj,
            JsonSerializerOptions? jsonOptions,
            IReadOnlyDictionary<string, string> typeHints)
            => ToUrlEncodedStringCore(obj, typeHints, jsonOptions);

        private static string ToUrlEncodedStringCore(
            object obj,
            IReadOnlyDictionary<string, string>? typeHints,
            JsonSerializerOptions? jsonOptions)
        {
#if NET10_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(obj);
#else
            if(obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
#endif

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

                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? property.Name;

                name = JsonNamingPolicy.CamelCase.ConvertName(name);

                AddValue(pairs, name, propertyValue, jsonOptions);
            }

            AddTypeHints(pairs, typeHints);

            return string.Join("&", pairs);
        }

        protected static void AddTypeHints(
            ICollection<string> pairs,
            IReadOnlyDictionary<string, string>? typeHints)
        {
            if (typeHints is null)
                return;

            foreach (var hint in typeHints)
            {
                if (string.IsNullOrWhiteSpace(hint.Key))
                    throw new ArgumentException("Il nome del parametro non può essere vuoto.", nameof(typeHints));

                if (string.IsNullOrWhiteSpace(hint.Value))
                    throw new ArgumentException(
                        $"Il tipo del parametro '{hint.Key}' non può essere vuoto.",
                        nameof(typeHints));

                AddPair(pairs, TypePrefix + hint.Key, hint.Value.Trim());
            }
        }

        protected static void AddPair(ICollection<string> pairs, string name, string value)
        {
            pairs.Add($"{WebUtility.UrlEncode(name)}={WebUtility.UrlEncode(value)}");
        }

        protected static void AddValue(
            ICollection<string> pairs,
            string name,
            object value,
            JsonSerializerOptions? jsonOptions)
        {
            if (value is JsonElement jsonElement)
            {
                AddJsonElement(pairs, name, jsonElement);
                return;
            }

            if (value is Array array)
            {
                foreach (var item in array)
                {
                    if (item is not null)
                        AddValue(pairs, name, item, jsonOptions);
                }

                return;
            }

            if (TryConvertToInvariantString(value, out var converted))
            {
                AddPair(pairs, name, converted!);
                return;
            }

            AddPair(
                pairs,
                name,
                JsonSerializer.Serialize(value, value.GetType(), jsonOptions));
        }

        private static void AddJsonElement(
            ICollection<string> pairs,
            string name,
            JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return;

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                    AddJsonElement(pairs, name, item);

                return;
            }

            AddPair(
                pairs,
                name,
                value.ValueKind == JsonValueKind.String
                    ? value.GetString()!
                    : value.GetRawText());
        }

        protected static bool IsExtensionDataProperty(
            object model,
            PropertyInfo property)
            => property.GetCustomAttribute<JsonExtensionDataAttribute>() is not null ||
               model is IWithUnmanagedData &&
               property.Name == nameof(IWithUnmanagedData.OtherData);

        protected static string ConvertToInvariantString(object value)
        {
            if (TryConvertToInvariantString(value, out var converted))
                return converted!;

            throw new NotSupportedException(
                $"Il tipo '{value.GetType().FullName}' non può essere convertito in query string.");
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
    }
}
