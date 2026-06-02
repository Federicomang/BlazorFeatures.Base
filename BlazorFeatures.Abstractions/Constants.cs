using BlazorFeatures.Abstractions.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BlazorFeatures.Abstractions
{
    public class Constants
    {
        public const string ApplicationRenderTypeKey = "FeatureApplicationRenderType";
        public const string JsonSerializerOptionsKey = "FeatureJsonSerializerOptions";

        public static bool IsClientEnvironment => RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
        public static bool IsServerEnvironment => !IsClientEnvironment;

        public static JsonSerializerOptions DefaultJsonSerializerOptions
        {
            get => field;
            internal set
            {
                field = new(value);
                field.MakeReadOnly();
            }
        } = CreateDefaultJsonSerializerOptions();

        private static JsonSerializerOptions CreateDefaultJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            options.MakeReadOnly();
            return options;
        }

        public static IComponentRenderMode? GetApplicationRenderMode(RenderType renderType)
        {
            return renderType switch
            {
                RenderType.Client => new InteractiveWebAssemblyRenderMode(),
                RenderType.Server => new InteractiveServerRenderMode(),
                _ => null,
            };
        }

        public class RenderModes
        {
            public const string Static = "Static";
            public const string Server = "Server";
            public const string WebAssembly = "WebAssembly";
        }
    }
}
