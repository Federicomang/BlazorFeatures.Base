namespace BlazorFeatures.Abstractions.Server
{
    public class BrowserDoRequest<T> where T : class
    {
        public Dictionary<string, string> Headers { get; set; } = [];

        public int StatusCode { get; set; }

        public bool Redirected { get; set; }

        public T? Result { get; set; }
    }
}
