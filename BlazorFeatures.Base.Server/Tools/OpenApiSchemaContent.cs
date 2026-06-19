namespace BlazorFeatures.Base.Server.Tools
{
    public class OpenApiSchemaContent(Type requestType, string contentType = "application/json")
    {
        public Type RequestType { get; } = requestType;
        public string ContentType { get; } = contentType;
    }
}
