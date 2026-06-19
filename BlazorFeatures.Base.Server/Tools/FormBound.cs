using BlazorFeatures.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
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
            var names = new List<string>();

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                var name = JsonNamingPolicy.CamelCase.ConvertName(
                    prop.GetCustomAttribute<FromFormAttribute>()?.Name
                        ?? prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                        ?? prop.Name);

                names.Add(name);

                BindProperty(model, prop, name, form);
            }

            if(model is IWithUnmanagedData unmanaged)
            {
                foreach(var field in form)
                {
                    if(!names.Contains(field.Key))
                    {
                        unmanaged.OtherData ??= [];
                        unmanaged.OtherData.Add(field.Key, JsonSerializer.SerializeToElement(field.Value));
                    }
                }
            }

            return new FormBound<T>(model);
        }

        private static void BindProperty(
            T model,
            PropertyInfo prop,
            string name,
            IFormCollection form)
        {
            if (typeof(IFormFile).IsAssignableFrom(prop.PropertyType))
            {
                prop.SetValue(model, form.Files.GetFile(name));
                return;
            }

            if (!form.TryGetValue(name, out StringValues values))
                return;

            var raw = values.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(raw))
                return;

            var targetType =
                Nullable.GetUnderlyingType(prop.PropertyType)
                ?? prop.PropertyType;

            object? value =
                targetType.IsEnum
                    ? Enum.Parse(targetType, raw, true)
                    : TypeDescriptor.GetConverter(targetType)
                        .ConvertFromInvariantString(raw);

            prop.SetValue(model, value);
        }

        public static implicit operator T(FormBound<T> formBound)
            => formBound.Value;
    }
}
