using BlazorFeatures.Base.Server.Tools;

namespace BlazorFeatures.Base.Server.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ExplicitOpenApiRequestAttribute(OpenApiSchemaContent content) : Attribute
    {
        public Type RequestType => content.RequestType;
        public string ContentType => content.ContentType;
    }
}
