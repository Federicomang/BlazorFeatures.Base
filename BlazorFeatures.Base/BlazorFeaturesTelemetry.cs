using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BlazorFeatures.Base
{
    /// <summary>
    /// Exposes the standard .NET diagnostics produced by BlazorFeatures.
    /// Consumers can subscribe with OpenTelemetry or any compatible listener.
    /// </summary>
    public static class BlazorFeaturesTelemetry
    {
        public const string ActivitySourceName = "BlazorFeatures";
        public const string MeterName = "BlazorFeatures";

        public const string ExecutionCounterName = "blazorfeatures.feature.executions";
        public const string FailureCounterName = "blazorfeatures.feature.failures";
        public const string DurationHistogramName = "blazorfeatures.feature.duration";

        private static readonly string? Version =
            typeof(BlazorFeaturesTelemetry).Assembly.GetName().Version?.ToString();

        public static ActivitySource ActivitySource { get; } =
            new(ActivitySourceName, Version);

        public static Meter Meter { get; } =
            new(MeterName, Version);

        internal static Counter<long> ExecutionCounter { get; } = Meter.CreateCounter<long>(
            ExecutionCounterName,
            unit: "{execution}",
            description: "Number of feature executions.");

        internal static Counter<long> FailureCounter { get; } = Meter.CreateCounter<long>(
            FailureCounterName,
            unit: "{failure}",
            description: "Number of unsuccessful feature executions.");

        internal static Histogram<double> DurationHistogram { get; } = Meter.CreateHistogram<double>(
            DurationHistogramName,
            unit: "ms",
            description: "Feature execution duration in milliseconds.");
    }
}
