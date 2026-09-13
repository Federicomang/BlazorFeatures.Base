using BlazorFeatures.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Base.Server.Tools
{
    public sealed record QueryBoundMetadata(Type ModelType);

    public sealed class QueryBound<T> : IEndpointParameterMetadataProvider
        where T : new()
    {
        public T Value { get; }

        private QueryBound(T value)
        {
            Value = value;
        }

        public static void PopulateMetadata(
            ParameterInfo parameter,
            EndpointBuilder builder)
        {
            builder.Metadata.Add(
                new QueryBoundMetadata(typeof(T)));
        }

        public static ValueTask<QueryBound<T>?> BindAsync(
            HttpContext context,
            ParameterInfo parameter)
        {
            var query = context.Request.Query;
            var model = new T();
            var typeHints = UnmanagedDataBindingTools.GetTypeHints(query);

            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var jsonOptions = context.RequestServices
                .GetService<IOptions<JsonOptions>>()?
                .Value
                .SerializerOptions;

            foreach (var prop in typeof(T).GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                if (IsExtensionDataProperty(model, prop))
                    continue;

                var name = GetQueryParameterName(prop);

                names.Add(name);

                BindProperty(
                    model,
                    prop,
                    name,
                    query,
                    jsonOptions);
            }

            UnmanagedDataBindingTools.ValidateHints(
                typeHints,
                names,
                query,
                model is IWithUnmanagedData);

            if (model is IWithUnmanagedData unmanaged)
            {
                foreach (var field in query)
                {
                    if (names.Contains(field.Key) ||
                        UnmanagedDataBindingTools.IsTypeHint(field.Key))
                        continue;

                    if (field.Value.Count == 0)
                        continue;

                    unmanaged.OtherData ??= [];

                    unmanaged.OtherData[field.Key] = typeHints.TryGetValue(field.Key, out var typeName)
                        ? UnmanagedDataBindingTools.ConvertValues(field.Value, typeName, jsonOptions)
                        : ParseExtensionValues(field.Value, jsonOptions);
                }
            }

            return ValueTask.FromResult<QueryBound<T>?>(
                new QueryBound<T>(model));
        }

        private static JsonElement ParseExtensionValue(
            string raw,
            JsonSerializerOptions? jsonOptions)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(
                    raw,
                    jsonOptions);
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(
                    raw,
                    jsonOptions);
            }
        }

        private static JsonElement ParseExtensionValues(
            StringValues values,
            JsonSerializerOptions? jsonOptions)
        {
            if (values.Count == 1)
                return ParseExtensionValue(values[0] ?? string.Empty, jsonOptions);

            var parsedValues = new List<JsonElement>(values.Count);

            foreach (var value in values)
                parsedValues.Add(ParseExtensionValue(value ?? string.Empty, jsonOptions));

            return JsonSerializer.SerializeToElement(parsedValues, jsonOptions);
        }

        private static bool IsExtensionDataProperty(
            T model,
            PropertyInfo property)
            => property.GetCustomAttribute<JsonExtensionDataAttribute>() is not null ||
               model is IWithUnmanagedData &&
               property.Name == nameof(IWithUnmanagedData.OtherData);

        private static string GetQueryParameterName(
            PropertyInfo property)
        {
            var name = property.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromQueryAttribute>()?.Name ??
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                property.Name;

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }

        private static void BindProperty(
            T model,
            PropertyInfo prop,
            string name,
            IQueryCollection query,
            JsonSerializerOptions? jsonOptions)
        {
            if (!query.TryGetValue(name, out StringValues values))
                return;

            try
            {
                if (prop.PropertyType.IsArray)
                {
                    BindArray(
                        model,
                        prop,
                        name,
                        values,
                        jsonOptions);

                    return;
                }

                var raw = values.FirstOrDefault();

                if (raw is null)
                    return;

                var nullableType =
                    Nullable.GetUnderlyingType(prop.PropertyType);

                var targetType =
                    nullableType ?? prop.PropertyType;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (targetType == typeof(string))
                    {
                        prop.SetValue(model, raw);
                        return;
                    }

                    if (nullableType is not null)
                    {
                        prop.SetValue(model, null);
                        return;
                    }

                    throw new FormatException(
                        $"Il parametro '{name}' non può essere vuoto.");
                }

                var value = ConvertValue(
                    raw,
                    targetType,
                    jsonOptions);

                prop.SetValue(model, value);
            }
            catch (Exception ex) when (
                ex is FormatException
                or InvalidCastException
                or NotSupportedException
                or ArgumentException
                or JsonException)
            {
                throw new BadHttpRequestException(
                    $"Il parametro query '{name}' non è valido " +
                    $"per la proprietà '{prop.Name}' di tipo " +
                    $"'{prop.PropertyType.Name}'.",
                    ex);
            }
        }

        private static void BindArray(
            T model,
            PropertyInfo prop,
            string name,
            StringValues values,
            JsonSerializerOptions? jsonOptions)
        {
            var elementType =
                prop.PropertyType.GetElementType()
                ?? throw new InvalidOperationException(
                    $"Impossibile determinare il tipo degli elementi di '{prop.Name}'.");

            var array = Array.CreateInstance(
                elementType,
                values.Count);

            for (var index = 0; index < values.Count; index++)
            {
                var raw = values[index];

                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new FormatException(
                        $"Il valore {index + 1} del parametro '{name}' è vuoto.");
                }

                var nullableElementType =
                    Nullable.GetUnderlyingType(elementType);

                var targetType =
                    nullableElementType ?? elementType;

                array.SetValue(
                    ConvertValue(raw, targetType, jsonOptions),
                    index);
            }

            prop.SetValue(model, array);
        }

        private static object? ConvertValue(
            string raw,
            Type targetType,
            JsonSerializerOptions? jsonOptions)
        {
            if (targetType == typeof(string))
                return raw;

            if (targetType.IsEnum)
            {
                return Enum.Parse(
                    targetType,
                    raw,
                    ignoreCase: true);
            }

            var converter =
                TypeDescriptor.GetConverter(targetType);

            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(raw);

            return JsonSerializer.Deserialize(raw, targetType, jsonOptions)
                ?? throw new JsonException(
                    $"Il JSON per il tipo '{targetType.Name}' ha prodotto un valore null.");
        }

        public static implicit operator T(
            QueryBound<T> queryBound)
            => queryBound.Value;
    }
}
