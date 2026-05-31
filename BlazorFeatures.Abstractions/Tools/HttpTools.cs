using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Abstractions.Tools
{
    public class HttpTools
    {
        public static string ToUrlEncodedString(object obj)
        {
            var pairs = obj.GetType()
                .GetProperties()
                .Where(p => p.GetValue(obj) != null)
                .Select(p =>
                {
                    var name = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name;

                    var value = Convert.ToString(p.GetValue(obj), CultureInfo.InvariantCulture)!;

                    return $"{WebUtility.UrlEncode(name)}={WebUtility.UrlEncode(value)}";
                });

            return string.Join("&", pairs);
        }
    }
}
