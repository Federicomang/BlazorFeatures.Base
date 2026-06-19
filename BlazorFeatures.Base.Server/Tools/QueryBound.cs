using BlazorFeatures.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.ComponentModel;
using System.Globalization;
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

        public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
        {
            builder.Metadata.Add(new QueryBoundMetadata(typeof(T)));
        }

        public static ValueTask<QueryBound<T>?> BindAsync(
            HttpContext context,
            ParameterInfo parameter)
        {
            var query = context.Request.Query;
            var model = new T();
            List<string> names = [];

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                var name = GetQueryParameterName(prop);
                names.Add(name);

                BindProperty(model, prop, name, query);
            }

            if (model is IWithUnmanagedData unmanaged)
            {
                foreach (var field in context.Request.Query)
                {
                    if (!names.Contains(field.Key))
                    {
                        unmanaged.OtherData ??= [];
                        unmanaged.OtherData.Add(field.Key, JsonSerializer.SerializeToElement(field.Value.ToString()));
                    }
                }
            }

            return ValueTask.FromResult<QueryBound<T>?>(
                new QueryBound<T>(model));
        }

        private static string GetQueryParameterName(PropertyInfo property)
        {
            var fromQuery = property.GetCustomAttribute<FromQueryAttribute>();

            string? propName;
            if (fromQuery == null)
            {
                propName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            }
            else
            {
                propName = fromQuery.Name ?? property.Name;
            }
            return JsonNamingPolicy.CamelCase.ConvertName(propName);
        }

        private static void BindProperty(
            T model,
            PropertyInfo prop,
            string name,
            IQueryCollection query)
        {
            if (!query.TryGetValue(name, out StringValues values))
                return;

            try
            {
                if (prop.PropertyType.IsArray)
                {
                    BindArray(model, prop, name, values);
                    return;
                }

                var raw = values.FirstOrDefault();

                if (raw is null)
                    return;

                var nullableType = Nullable.GetUnderlyingType(prop.PropertyType);
                var targetType = nullableType ?? prop.PropertyType;

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

                var value = ConvertValue(raw, targetType);

                prop.SetValue(model, value);
            }
            catch (Exception ex) when (
                ex is FormatException
                or InvalidCastException
                or NotSupportedException
                or ArgumentException)
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
            StringValues values)
        {
            var elementType = prop.PropertyType.GetElementType()
                ?? throw new InvalidOperationException(
                    $"Impossibile determinare il tipo degli elementi di '{prop.Name}'.");

            var array = Array.CreateInstance(elementType, values.Count);

            for (var index = 0; index < values.Count; index++)
            {
                var raw = values[index];

                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new FormatException(
                        $"Il valore {index + 1} del parametro '{name}' è vuoto.");
                }

                array.SetValue(
                    ConvertValue(raw, elementType),
                    index);
            }

            prop.SetValue(model, array);
        }

        private static object? ConvertValue(string raw, Type targetType)
        {
            if (targetType == typeof(string))
                return raw;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, raw, ignoreCase: true);

            var converter = TypeDescriptor.GetConverter(targetType);

            if (!converter.CanConvertFrom(typeof(string)))
            {
                throw new NotSupportedException(
                    $"Il tipo '{targetType.Name}' non può essere convertito da stringa.");
            }

            return converter.ConvertFrom(
                context: null,
                culture: CultureInfo.InvariantCulture,
                value: raw);
        }

        public static implicit operator T(QueryBound<T> queryBound)
            => queryBound.Value;
    }
}
