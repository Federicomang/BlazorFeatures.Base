using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;

namespace BlazorFeatures.Base.Server.Tools
{
    public class HttpTools : Abstractions.Tools.HttpTools
    {
        public new static string ToUrlEncodedString(object obj)
        {
            var pairs = obj.GetType()
                .GetProperties()
                .Where(p => p.GetValue(obj) != null)
                .Select(p =>
                {
                    var name =
                        p.GetCustomAttribute<FromFormAttribute>()?.Name ??
                        p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                        p.Name;

                    var value = Convert.ToString(p.GetValue(obj), CultureInfo.InvariantCulture)!;

                    return $"{WebUtility.UrlEncode(name)}={WebUtility.UrlEncode(value)}";
                });

            return string.Join("&", pairs);
        }

        public static string BuildServerApi(string s, [StringSyntax("Route")] string route)
        {
            return string.Format(s, args: route.Split(';'));
        }
    }
}
