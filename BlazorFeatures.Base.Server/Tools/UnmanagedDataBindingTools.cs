using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using BlazorFeatures.Abstractions.Tools;

namespace BlazorFeatures.Base.Server.Tools;

internal static class UnmanagedDataBindingTools
{
    private static readonly Dictionary<string, Type> TypeAliases = new(
        StringComparer.OrdinalIgnoreCase)
    {
        [UrlEncodedValueTypes.String] = typeof(string),
        [UrlEncodedValueTypes.Boolean] = typeof(bool),
        ["boolean"] = typeof(bool),
        [UrlEncodedValueTypes.Byte] = typeof(byte),
        [UrlEncodedValueTypes.SignedByte] = typeof(sbyte),
        [UrlEncodedValueTypes.Int16] = typeof(short),
        [UrlEncodedValueTypes.UInt16] = typeof(ushort),
        [UrlEncodedValueTypes.Int32] = typeof(int),
        [UrlEncodedValueTypes.UInt32] = typeof(uint),
        [UrlEncodedValueTypes.Int64] = typeof(long),
        [UrlEncodedValueTypes.UInt64] = typeof(ulong),
        [UrlEncodedValueTypes.Single] = typeof(float),
        ["single"] = typeof(float),
        [UrlEncodedValueTypes.Double] = typeof(double),
        [UrlEncodedValueTypes.Decimal] = typeof(decimal),
        [UrlEncodedValueTypes.Char] = typeof(char),
        [UrlEncodedValueTypes.Guid] = typeof(Guid),
        [UrlEncodedValueTypes.DateTime] = typeof(DateTime),
        [UrlEncodedValueTypes.DateTimeOffset] = typeof(DateTimeOffset),
        [UrlEncodedValueTypes.DateOnly] = typeof(DateOnly),
        [UrlEncodedValueTypes.TimeOnly] = typeof(TimeOnly),
        [UrlEncodedValueTypes.TimeSpan] = typeof(TimeSpan),
        [UrlEncodedValueTypes.Uri] = typeof(Uri),
        [UrlEncodedValueTypes.Json] = typeof(JsonElement),
        ["jsonelement"] = typeof(JsonElement)
    };

    public static Dictionary<string, string> GetTypeHints(
        IEnumerable<KeyValuePair<string, StringValues>> fields)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (!TryGetHintedParameterName(field.Key, out var parameterName))
                continue;

            if (string.IsNullOrWhiteSpace(parameterName) ||
                parameterName.StartsWith(HttpTools.TypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadHttpRequestException(
                    $"Il parametro di tipo '{field.Key}' non contiene un nome valido.");
            }

            if (field.Value.Count != 1 || string.IsNullOrWhiteSpace(field.Value[0]))
            {
                throw new BadHttpRequestException(
                    $"Il parametro di tipo '{field.Key}' deve contenere un solo valore.");
            }

            var typeName = field.Value[0]!.Trim();
            ResolveType(typeName);

            if (!result.TryAdd(parameterName, typeName))
            {
                throw new BadHttpRequestException(
                    $"Il tipo del parametro '{parameterName}' è stato specificato più volte.");
            }
        }

        return result;
    }

    public static bool IsTypeHint(string name)
        => name.StartsWith(HttpTools.TypePrefix, StringComparison.OrdinalIgnoreCase);

    public static JsonElement ConvertValues(
        StringValues values,
        string typeName,
        JsonSerializerOptions? jsonOptions)
    {
        var (elementType, explicitlyArray) = ResolveType(typeName);
        var asArray = explicitlyArray || values.Count > 1;

        try
        {
            if (!asArray)
            {
                return ConvertValue(
                    values.FirstOrDefault(),
                    elementType,
                    jsonOptions);
            }

            var converted = new List<JsonElement>(values.Count);

            foreach (var value in values)
                converted.Add(ConvertValue(value, elementType, jsonOptions));

            return JsonSerializer.SerializeToElement(converted, jsonOptions);
        }
        catch (Exception ex) when (
            ex is FormatException
            or InvalidCastException
            or NotSupportedException
            or ArgumentException
            or JsonException
            or OverflowException)
        {
            throw new BadHttpRequestException(
                $"Il valore non è valido per il tipo dichiarato '{typeName}'.",
                ex);
        }
    }

    public static void ValidateHints(
        IReadOnlyDictionary<string, string> typeHints,
        ISet<string> managedNames,
        IEnumerable<KeyValuePair<string, StringValues>> fields,
        bool supportsUnmanagedData)
    {
        if (typeHints.Count == 0)
            return;

        if (!supportsUnmanagedData)
        {
            throw new BadHttpRequestException(
                "Gli hint di tipo possono essere usati solo con modelli che implementano IWithUnmanagedData.");
        }

        var fieldNames = new HashSet<string>(
            fields.Where(field => !IsTypeHint(field.Key)).Select(field => field.Key),
            StringComparer.OrdinalIgnoreCase);

        foreach (var hint in typeHints)
        {
            if (managedNames.Contains(hint.Key))
            {
                throw new BadHttpRequestException(
                    $"Non è possibile modificare il tipo del parametro dichiarato '{hint.Key}'.");
            }

            if (!fieldNames.Contains(hint.Key))
            {
                throw new BadHttpRequestException(
                    $"È stato specificato un tipo per il parametro mancante '{hint.Key}'.");
            }
        }
    }

    private static (Type Type, bool IsArray) ResolveType(string typeName)
    {
        var normalized = typeName.Trim();
        var isArray = normalized.EndsWith("[]", StringComparison.Ordinal);
        var elementTypeName = isArray ? normalized[..^2].Trim() : normalized;

        if (string.IsNullOrWhiteSpace(elementTypeName) ||
            !TypeAliases.TryGetValue(elementTypeName, out var type))
        {
            throw new BadHttpRequestException(
                $"Il tipo '{elementTypeName}' non è supportato.");
        }

        return (type, isArray);
    }

    private static JsonElement ConvertValue(
        string? rawValue,
        Type targetType,
        JsonSerializerOptions? jsonOptions)
    {
        var raw = rawValue ?? string.Empty;

        if (targetType == typeof(string))
            return JsonSerializer.SerializeToElement(raw, jsonOptions);

        if (targetType == typeof(JsonElement))
            return JsonSerializer.Deserialize<JsonElement>(raw, jsonOptions);

        object converted = targetType == typeof(Guid)
            ? Guid.Parse(raw)
            : targetType == typeof(DateTime)
                ? DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : targetType == typeof(DateTimeOffset)
                    ? DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    : targetType == typeof(DateOnly)
                        ? DateOnly.Parse(raw, CultureInfo.InvariantCulture)
                        : targetType == typeof(TimeOnly)
                            ? TimeOnly.Parse(raw, CultureInfo.InvariantCulture)
                            : targetType == typeof(TimeSpan)
                                ? TimeSpan.Parse(raw, CultureInfo.InvariantCulture)
                                : targetType == typeof(Uri)
                                    ? new Uri(raw, UriKind.RelativeOrAbsolute)
                                    : ConvertWithTypeConverter(raw, targetType);

        return JsonSerializer.SerializeToElement(converted, targetType, jsonOptions);
    }

    private static object ConvertWithTypeConverter(string raw, Type targetType)
    {
        var converter = TypeDescriptor.GetConverter(targetType);

        if (!converter.CanConvertFrom(typeof(string)))
        {
            throw new NotSupportedException(
                $"Il tipo '{targetType.Name}' non può essere convertito da stringa.");
        }

        return converter.ConvertFromInvariantString(raw)
            ?? throw new InvalidCastException(
                $"La conversione nel tipo '{targetType.Name}' ha prodotto null.");
    }

    private static bool TryGetHintedParameterName(string name, out string parameterName)
    {
        if (!IsTypeHint(name))
        {
            parameterName = string.Empty;
            return false;
        }

        parameterName = name[HttpTools.TypePrefix.Length..];
        return true;
    }
}
