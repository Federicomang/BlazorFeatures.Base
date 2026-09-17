using BlazorFeatures.Abstractions.Tools;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Abstractions.JsonConverters
{
    public class ObjectValueJsonConverter<T> : JsonConverter<ObjectValue<T>>
    {
        public override ObjectValue<T>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<T>(ref reader, options);

            return new ObjectValue<T>(value!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ObjectValue<T> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.GetValue(), options);
        }
    }

    public class ObjectValueJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType &&
                   typeToConvert.GetGenericTypeDefinition() == typeof(ObjectValue<>);
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var valueType = typeToConvert.GetGenericArguments()[0];

            var converterType = typeof(ObjectValueJsonConverter<>)
                .MakeGenericType(valueType);

            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    public class AsyncObjectValueJsonConverter<T> : JsonConverter<AsyncObjectValue<T>>
    {
        public override AsyncObjectValue<T>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<T>(ref reader, options);

            return new AsyncObjectValue<T>(value!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            AsyncObjectValue<T> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.GetValue(), options);
        }
    }

    public class AsyncObjectValueJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType &&
                   typeToConvert.GetGenericTypeDefinition() == typeof(AsyncObjectValue<>);
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var valueType = typeToConvert.GetGenericArguments()[0];

            var converterType = typeof(AsyncObjectValueJsonConverter<>)
                .MakeGenericType(valueType);

            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}
