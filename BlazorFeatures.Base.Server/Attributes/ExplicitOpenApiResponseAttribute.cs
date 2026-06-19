using BlazorFeatures.Base.Server.Tools;

namespace BlazorFeatures.Base.Server.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExplicitOpenApiResponseAttribute(int statusCode, params OpenApiSchemaContent[] contents) : Attribute
    {
        public int StatusCode { get; } = statusCode;
        public OpenApiSchemaContent[] Contents { get; } = contents;

        public string? Description { get; set; }
    }
}
