using BlazorFeatures.Abstractions.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Runtime.InteropServices;

namespace BlazorFeatures.Base
{
    public class Constants
    {
        public const string ApplicationRenderTypeKey = "FeatureApplicationRenderType";
        public const string JsonSerializerOptionsKey = "FeatureJsonSerializerOptions";

        public static bool IsClientEnvironment => RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
        public static bool IsServerEnvironment => !IsClientEnvironment;

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
