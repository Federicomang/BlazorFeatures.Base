using BlazorFeatures.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Tests;

public sealed class FeatureScopeLifetimeTests
{
    [Fact]
    public async Task Deferred_operation_keeps_scope_alive_until_it_completes()
    {
        var recorder = new ScopeLifetimeRecorder();
        using var provider = CreateProvider(recorder);
        var deferredOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var response = await provider.GetRequiredService<IFeatureService>()
            .Run(new DeferredRequest(deferredOperation.Task));

        Assert.True(response.Success);
        Assert.False(recorder.ScopeDisposed.Task.IsCompleted);

        deferredOperation.SetResult();
        await recorder.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, recorder.DisposeCount);
    }

    [Fact]
    public async Task All_deferred_operations_must_complete_before_scope_disposal()
    {
        var recorder = new ScopeLifetimeRecorder();
        using var provider = CreateProvider(recorder);
        var firstOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var response = await provider.GetRequiredService<IFeatureService>()
            .Run(new DeferredRequest(firstOperation.Task, secondOperation.Task));

        Assert.True(response.Success);
        firstOperation.SetResult();
        await Task.Yield();
        Assert.False(recorder.ScopeDisposed.Task.IsCompleted);

        secondOperation.SetResult();
        await recorder.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, recorder.DisposeCount);
    }

    [Fact]
    public async Task Failed_deferred_operation_is_observed_and_scope_is_disposed()
    {
        var recorder = new ScopeLifetimeRecorder();
        using var provider = CreateProvider(recorder);

        var response = await provider.GetRequiredService<IFeatureService>()
            .Run(new DeferredRequest(Task.FromException(
                new InvalidOperationException("Deferred failure"))));

        Assert.True(response.Success);
        await recorder.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, recorder.DisposeCount);
    }

    [Fact]
    public async Task Child_can_reuse_parent_scope_and_extend_its_lifetime()
    {
        var recorder = new ScopeLifetimeRecorder();
        using var provider = CreateProvider(recorder);
        var deferredOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var response = await provider.GetRequiredService<IFeatureService>()
            .Run(new ReuseParentRequest(deferredOperation.Task));

        Assert.True(response.Success);
        Assert.Equal(recorder.ParentScopeId, recorder.ChildScopeId);
        Assert.False(recorder.ChildReusesScopeForItsOwnChildren);
        Assert.False(recorder.ScopeDisposed.Task.IsCompleted);

        deferredOperation.SetResult();
        await recorder.ScopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, recorder.DisposeCount);
    }

    private static ServiceProvider CreateProvider(ScopeLifetimeRecorder recorder)
    {
        var services = new ServiceCollection();
        services.AddScoped<IFeatureService, FeatureService>();
        services.AddScoped<TrackedScopedDependency>();
        services.AddScoped<IBaseFeature<DeferredRequest, LifetimeResponse>, DeferredFeature>();
        services.AddScoped<IBaseFeature<ReuseParentRequest, LifetimeResponse>, ReuseParentFeature>();
        services.AddScoped<IBaseFeature<ReuseChildRequest, LifetimeResponse>, ReuseChildFeature>();
        services.AddSingleton(recorder);
        return services.BuildServiceProvider();
    }

    public sealed record DeferredRequest(params Task[] Operations) :
        IBaseFeatureRequest<LifetimeResponse>;

    public sealed record ReuseParentRequest(Task ChildOperation) :
        IBaseFeatureRequest<LifetimeResponse>;

    public sealed record ReuseChildRequest(Task Operation) :
        IBaseFeatureRequest<LifetimeResponse>;

    public sealed class LifetimeResponse;

    public sealed class DeferredFeature(TrackedScopedDependency dependency) :
        IBaseFeature<DeferredRequest, LifetimeResponse>
    {
        public Task<FeatureResponse<LifetimeResponse>> HandleClient(
            DeferredRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(request, featureContext);

        public Task<FeatureResponse<LifetimeResponse>> HandleServer(
            DeferredRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(request, featureContext);

        private Task<FeatureResponse<LifetimeResponse>> Handle(
            DeferredRequest request,
            IFeatureContext featureContext)
        {
            _ = dependency.Id;
            foreach (var operation in request.Operations)
                featureContext.DeferScopeDisposalUntil(operation);
            return Success();
        }
    }

    public sealed class ReuseParentFeature(
        IFeatureService featureService,
        TrackedScopedDependency dependency,
        ScopeLifetimeRecorder recorder) :
        IBaseFeature<ReuseParentRequest, LifetimeResponse>
    {
        public Task<FeatureResponse<LifetimeResponse>> HandleClient(
            ReuseParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(request, featureContext, cancellationToken);

        public Task<FeatureResponse<LifetimeResponse>> HandleServer(
            ReuseParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(request, featureContext, cancellationToken);

        private async Task<FeatureResponse<LifetimeResponse>> Handle(
            ReuseParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken)
        {
            recorder.ParentScopeId = dependency.Id;
            featureContext.UseSameServiceScope = true;
            return await featureService.Run(
                new ReuseChildRequest(request.ChildOperation),
                featureContext,
                cancellationToken);
        }
    }

    public sealed class ReuseChildFeature(
        TrackedScopedDependency dependency,
        ScopeLifetimeRecorder recorder) :
        IBaseFeature<ReuseChildRequest, LifetimeResponse>
    {
        public Task<FeatureResponse<LifetimeResponse>> HandleClient(
            ReuseChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(request, featureContext);

        public Task<FeatureResponse<LifetimeResponse>> HandleServer(
            ReuseChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(request, featureContext);

        private Task<FeatureResponse<LifetimeResponse>> Handle(
            ReuseChildRequest request,
            IFeatureContext featureContext)
        {
            recorder.ChildScopeId = dependency.Id;
            recorder.ChildReusesScopeForItsOwnChildren = featureContext.UseSameServiceScope;
            featureContext.DeferScopeDisposalUntil(request.Operation);
            return Success();
        }
    }

    public sealed class TrackedScopedDependency(ScopeLifetimeRecorder recorder) : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref recorder.DisposeCount);
            recorder.ScopeDisposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    public sealed class ScopeLifetimeRecorder
    {
        public Guid ParentScopeId { get; set; }

        public Guid ChildScopeId { get; set; }

        public bool ChildReusesScopeForItsOwnChildren { get; set; }

        public int DisposeCount;

        public TaskCompletionSource ScopeDisposed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static Task<FeatureResponse<LifetimeResponse>> Success() =>
        Task.FromResult(FeatureResponse<LifetimeResponse>.AsSuccess(new()));
}
