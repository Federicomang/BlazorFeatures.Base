using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BlazorFeatures.Base.Tests;

public class FeatureTelemetryTests
{
    [Fact]
    public async Task Run_emits_an_activity_without_recording_the_payload()
    {
        Activity? recordedActivity = null;
        using var listener = CreateActivityListener(activity => recordedActivity = activity);
        using var provider = CreateProvider(services =>
            services.AddScoped<IBaseFeature<TelemetryRequest, TelemetryResponse>, SuccessfulFeature>());
        using var scope = provider.CreateScope();
        var context = new BaseFeatureContext();

        var response = await scope.ServiceProvider.GetRequiredService<IFeatureService>()
            .Run(new TelemetryRequest("do-not-record"), context);

        Assert.True(response.Success);
        Assert.NotNull(recordedActivity);
        Assert.Equal(ActivityStatusCode.Ok, recordedActivity.Status);
        Assert.Equal(typeof(TelemetryRequest).FullName, recordedActivity.GetTagItem("blazorfeatures.request.type"));
        Assert.Equal(context.OperationId.ToString(), recordedActivity.GetTagItem("blazorfeatures.operation_id"));
        Assert.Equal("success", recordedActivity.GetTagItem("blazorfeatures.outcome"));
        Assert.DoesNotContain(recordedActivity.TagObjects, tag => Equals(tag.Value, "do-not-record"));
    }

    [Fact]
    public async Task Nested_features_emit_parent_and_child_activities()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = CreateActivityListener(activities.Add);
        using var provider = CreateProvider(services =>
        {
            services.AddScoped<IBaseFeature<ParentRequest, TelemetryResponse>, ParentFeature>();
            services.AddScoped<IBaseFeature<ChildRequest, TelemetryResponse>, ChildFeature>();
        });
        using var scope = provider.CreateScope();

        var response = await scope.ServiceProvider.GetRequiredService<IFeatureService>()
            .Run(new ParentRequest());

        Assert.True(response.Success);
        var parent = Assert.Single(activities, activity => activity.DisplayName == $"Feature {nameof(ParentRequest)}");
        var child = Assert.Single(activities, activity => activity.DisplayName == $"Feature {nameof(ChildRequest)}");
        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.SpanId, child.ParentSpanId);
        Assert.Equal(parent.GetTagItem("blazorfeatures.operation_id"), child.GetTagItem("blazorfeatures.operation_id"));
    }

    [Fact]
    public async Task Exception_details_are_redacted_by_default()
    {
        Activity? recordedActivity = null;
        using var listener = CreateActivityListener(activity => recordedActivity = activity);
        using var provider = CreateProvider(services =>
            services.AddScoped<IBaseFeature<TelemetryRequest, TelemetryResponse>, ThrowingFeature>());
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IFeatureService>()
                .Run(new TelemetryRequest("secret")));

        Assert.NotNull(recordedActivity);
        Assert.Equal(ActivityStatusCode.Error, recordedActivity.Status);
        Assert.Equal(typeof(InvalidOperationException).FullName, recordedActivity.GetTagItem("exception.type"));
        Assert.Null(recordedActivity.GetTagItem("exception.message"));
        Assert.Null(recordedActivity.GetTagItem("exception.stacktrace"));
        Assert.Equal("error", recordedActivity.GetTagItem("blazorfeatures.outcome"));
    }

    [Fact]
    public async Task Run_emits_execution_and_duration_metrics()
    {
        var measurements = new ConcurrentBag<(string Instrument, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == BlazorFeaturesTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        using var provider = CreateProvider(services =>
            services.AddScoped<IBaseFeature<TelemetryRequest, TelemetryResponse>, SuccessfulFeature>());
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IFeatureService>()
            .Run(new TelemetryRequest("payload"));

        Assert.Contains(measurements, measurement =>
            measurement.Instrument == BlazorFeaturesTelemetry.ExecutionCounterName
            && measurement.Value == 1);
        Assert.Contains(measurements, measurement =>
            measurement.Instrument == BlazorFeaturesTelemetry.DurationHistogramName
            && measurement.Value >= 0
            && measurement.Tags.Any(tag => tag.Key == "blazorfeatures.outcome" && Equals(tag.Value, "success")));
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddOptions<FeatureTelemetryOptions>();
        services.AddScoped<IFeatureService, FeatureService>();
        configure(services);
        return services.BuildServiceProvider();
    }

    private static ActivityListener CreateActivityListener(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BlazorFeaturesTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onStopped
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public sealed record TelemetryRequest(string Secret) : IBaseFeatureRequest<TelemetryResponse>;

    public sealed class ParentRequest : IBaseFeatureRequest<TelemetryResponse>;

    public sealed class ChildRequest : IBaseFeatureRequest<TelemetryResponse>;

    public sealed class TelemetryResponse;

    public sealed class SuccessfulFeature : IBaseFeature<TelemetryRequest, TelemetryResponse>
    {
        public Task<FeatureResponse<TelemetryResponse>> HandleClient(
            TelemetryRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Success();

        public Task<FeatureResponse<TelemetryResponse>> HandleServer(
            TelemetryRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Success();
    }

    public sealed class ParentFeature(IFeatureService featureService) :
        IBaseFeature<ParentRequest, TelemetryResponse>
    {
        public Task<FeatureResponse<TelemetryResponse>> HandleClient(
            ParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            featureService.Run(new ChildRequest(), featureContext, cancellationToken);

        public Task<FeatureResponse<TelemetryResponse>> HandleServer(
            ParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            featureService.Run(new ChildRequest(), featureContext, cancellationToken);
    }

    public sealed class ChildFeature : IBaseFeature<ChildRequest, TelemetryResponse>
    {
        public Task<FeatureResponse<TelemetryResponse>> HandleClient(
            ChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Success();

        public Task<FeatureResponse<TelemetryResponse>> HandleServer(
            ChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Success();
    }

    public sealed class ThrowingFeature : IBaseFeature<TelemetryRequest, TelemetryResponse>
    {
        public Task<FeatureResponse<TelemetryResponse>> HandleClient(
            TelemetryRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Throw();

        public Task<FeatureResponse<TelemetryResponse>> HandleServer(
            TelemetryRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Throw();

        private static Task<FeatureResponse<TelemetryResponse>> Throw() =>
            throw new InvalidOperationException("sensitive exception message");
    }

    private static Task<FeatureResponse<TelemetryResponse>> Success() =>
        Task.FromResult(FeatureResponse<TelemetryResponse>.AsSuccess(new()));
}
