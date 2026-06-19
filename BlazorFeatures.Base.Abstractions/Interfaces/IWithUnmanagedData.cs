using System.Collections.Generic;
using System.Text.Json;

namespace BlazorFeatures.Abstractions.Interfaces
{
    public interface IWithUnmanagedData
    {
        public Dictionary<string, JsonElement>? OtherData { get; set; }
    }
}
