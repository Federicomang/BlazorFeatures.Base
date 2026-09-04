namespace BlazorFeatures.Base.Options
{
    /// <summary>
    /// Controls the vendor-neutral diagnostics emitted by BlazorFeatures.
    /// </summary>
    public sealed class FeatureTelemetryOptions
    {
        public bool TracingEnabled { get; set; } = true;

        public bool MetricsEnabled { get; set; } = true;

        /// <summary>
        /// Includes exception messages and stack traces in activities. Disabled by default
        /// because those values may contain sensitive application data.
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }
    }
}
