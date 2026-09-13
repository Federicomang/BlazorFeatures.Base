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
    public sealed record FormBoundMetadata(Type ModelType);

    public sealed class FormBound<T> : IEndpointParameterMetadataProvider where T : new()
    {
        public T Value { get; }

        private FormBound(T value)
        {
            Value = value;
        }

        public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
        {
            builder.Metadata.Add(new FormBoundMetadata(typeof(T)));
        }

        public static async ValueTask<FormBound<T>?> BindAsync(HttpContext context)
        {
            if (!context.Request.HasFormContentType)
                return null;

            var form = await context.Request.ReadFormAsync();

            var model = new T();
            var typeHints = UnmanagedDataBindingTools.GetTypeHints(form);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var jsonOptions = context.RequestServices
                .GetService<IOptions<JsonOptions>>()?
                .Value
                .SerializerOptions;

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                if (IsExtensionDataProperty(model, prop))
                    continue;

                var name = JsonNamingPolicy.CamelCase.ConvertName(
                    prop.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromFormAttribute>()?.Name
                        ?? prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                        ?? prop.Name);

                names.Add(name);

                BindProperty(model, prop, name, form, jsonOptions);
            }

            UnmanagedDataBindingTools.ValidateHints(
                typeHints,
                names,
                form,
                model is IWithUnmanagedData);

            if(model is IWithUnmanagedData unmanaged)
            {
                foreach (var field in form)
                {
                    if (!names.Contains(field.Key) &&
                        !UnmanagedDataBindingTools.IsTypeHint(field.Key))
                    {
                        unmanaged.OtherData ??= [];
                        unmanaged.OtherData[field.Key] = typeHints.TryGetValue(field.Key, out var typeName)
                            ? UnmanagedDataBindingTools.ConvertValues(field.Value, typeName, jsonOptions)
                            : ParseExtensionValues(field.Value, jsonOptions);
                    }
                }
            }

            return new FormBound<T>(model);
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

        private static void BindProperty(
            T model,
            PropertyInfo prop,
            string name,
            IFormCollection form,
            JsonSerializerOptions? jsonOptions)
        {
            if (typeof(IFormFile).IsAssignableFrom(prop.PropertyType))
            {
                prop.SetValue(model, form.Files.GetFile(name));
                return;
            }

            if (!form.TryGetValue(name, out StringValues values))
                return;

            if (prop.PropertyType.IsArray)
            {
                BindArray(model, prop, name, values, jsonOptions);
                return;
            }

            var raw = values.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(raw))
                return;

            var targetType =
                Nullable.GetUnderlyingType(prop.PropertyType)
                ?? prop.PropertyType;

            object? value =
                targetType.IsEnum
                    ? Enum.Parse(targetType, raw, true)
                    : ConvertValue(raw, targetType, jsonOptions);

            prop.SetValue(model, value);
        }

        private static void BindArray(
            T model,
            PropertyInfo prop,
            string name,
            StringValues values,
            JsonSerializerOptions? jsonOptions)
        {
            var elementType = prop.PropertyType.GetElementType()
                ?? throw new InvalidOperationException(
                    $"Impossibile determinare il tipo degli elementi di '{prop.Name}'.");
            var array = Array.CreateInstance(elementType, values.Count);

            for (var index = 0; index < values.Count; index++)
            {
                var raw = values[index];
                var targetType = Nullable.GetUnderlyingType(elementType) ?? elementType;

                if (string.IsNullOrWhiteSpace(raw) && targetType != typeof(string))
                    throw new FormatException(
                        $"Il valore {index + 1} del parametro '{name}' è vuoto.");

                array.SetValue(
                    ConvertValue(raw ?? string.Empty, targetType, jsonOptions),
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
                return Enum.Parse(targetType, raw, true);

            var converter = TypeDescriptor.GetConverter(targetType);

            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(raw);

            return JsonSerializer.Deserialize(raw, targetType, jsonOptions)
                ?? throw new JsonException(
                    $"Il JSON per il tipo '{targetType.Name}' ha prodotto un valore null.");
        }

        public static implicit operator T(FormBound<T> formBound)
            => formBound.Value;
    }
}
