using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Server;
using BlazorFeatures.Base.Server.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace BlazorFeatures.Base.Tests;

public class FeatureContextValuesTests
{
    [Fact]
    public void Values_combines_permanent_and_temporary_values_with_temporary_precedence()
    {
        var context = new BaseFeatureContext(new ParentRequest());
        context.PermanentValues["permanent"] = 1;
        context.PermanentValues["overridden"] = "permanent";
        context.TempValues["temporary"] = 2;
        context.TempValues["overridden"] = "temporary";

        Assert.Equal(1, context.Values["permanent"]);
        Assert.Equal(2, context.Values["temporary"]);
        Assert.Equal("temporary", context.Values["overridden"]);
        Assert.Equal(3, context.Values.Count);
    }

    [Fact]
    public async Task Temporary_values_reach_direct_feature_invocations_only()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var request = new ParentRequest();
        var context = new BaseFeatureContext(request);
        context.TempValues["root-temp"] = "root";
        context.PermanentValues["permanent"] = "initial";

        var response = await scope.ServiceProvider
            .GetRequiredService<IFeatureService>()
            .Run(request, context);

        Assert.True(response.Success);
        Assert.Equal("root", context.TempValues["root-temp"]);
        Assert.Equal("changed-by-child", context.PermanentValues["permanent"]);
    }

    [Fact]
    public async Task Http_context_is_preserved_in_invocation_scopes()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var httpContext = new DefaultHttpContext();
        var request = new HttpRequest();
        var context = new HttpFeatureContext(httpContext, request);

        var response = await scope.ServiceProvider
            .GetRequiredService<IFeatureService>()
            .Run(request, context);

        Assert.True(response.Success);
        Assert.Same(httpContext, HttpFeature.SeenHttpContext);
        Assert.True(context.TryGetHttpResult(request, out _));
        var node = Assert.Single(context.FeatureChain.Values);
        Assert.Null(node.ParentNodeId);
        Assert.Same(request, node.Request);
    }

    [Fact]
    public async Task Parallel_children_have_independent_scopes_and_receive_the_same_temporary_values()
    {
        using var provider = CreateProvider();
        var recorder = provider.GetRequiredService<ParallelExecutionRecorder>();
        var request = new ParallelParentRequest();
        var context = new BaseFeatureContext(request);

        var response = await provider.GetRequiredService<IFeatureService>()
            .Run(request, context);

        Assert.True(response.Success);
        var parentNode = Assert.Single(
            context.FeatureChain.Values,
            node => node.Request is ParallelParentRequest);
        var childNodes = context.FeatureChain.Values
            .Where(node => node.Request is ParallelChildRequest)
            .ToArray();
        Assert.Equal(2, childNodes.Length);
        Assert.All(childNodes, node => Assert.Equal(parentNode.NodeId, node.ParentNodeId));

        var observations = recorder.Observations.ToArray();
        Assert.Equal(2, observations.Length);
        Assert.All(observations, observation => Assert.Equal("shared", observation.TempValue));
        Assert.All(observations, observation => Assert.True(observation.RequestMatchesContext));
        Assert.Equal(2, observations.Select(observation => observation.ScopeId).Distinct().Count());
        Assert.Equal("one", context.PermanentValues["branch-1"]);
        Assert.Equal("two", context.PermanentValues["branch-2"]);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureService, FeatureService>();
        services.AddScoped<IBaseFeature<ParentRequest, TestResponse>, ParentFeature>();
        services.AddScoped<IBaseFeature<ChildRequest, TestResponse>, ChildFeature>();
        services.AddScoped<IBaseFeature<GrandchildRequest, TestResponse>, GrandchildFeature>();
        services.AddScoped<IBaseFeature<HttpRequest, TestResponse>, HttpFeature>();
        services.AddScoped<IBaseFeature<ParallelParentRequest, TestResponse>, ParallelParentFeature>();
        services.AddScoped<IBaseFeature<ParallelChildRequest, TestResponse>, ParallelChildFeature>();
        services.AddScoped<ScopedDependency>();
        services.AddSingleton<ParallelExecutionRecorder>();
        return services.BuildServiceProvider();
    }

    public sealed class ParentRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class ChildRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class GrandchildRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class HttpRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class ParallelParentRequest : IBaseFeatureRequest<TestResponse>;

    public sealed record ParallelChildRequest(int Branch) : IBaseFeatureRequest<TestResponse>;

    public sealed class TestResponse;

    public sealed class ParentFeature(IFeatureService featureService) :
        IBaseFeature<ParentRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            ParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            ParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        private async Task<FeatureResponse<TestResponse>> Handle(
            IFeatureContext featureContext,
            CancellationToken cancellationToken)
        {
            Assert.Equal("root", featureContext.Values["root-temp"]);
            Assert.Equal("initial", featureContext.Values["permanent"]);

            featureContext.TempValues["parent-temp"] = "parent";
            var response = await featureService.Run(
                new ChildRequest(),
                featureContext,
                cancellationToken);

            Assert.Equal("root", featureContext.Values["root-temp"]);
            Assert.Equal("parent", featureContext.Values["parent-temp"]);
            Assert.Equal("changed-by-child", featureContext.Values["permanent"]);
            return response;
        }
    }

    public sealed class ChildFeature(IFeatureService featureService) :
        IBaseFeature<ChildRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            ChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            ChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        private async Task<FeatureResponse<TestResponse>> Handle(
            IFeatureContext featureContext,
            CancellationToken cancellationToken)
        {
            Assert.False(featureContext.Values.ContainsKey("root-temp"));
            Assert.Equal("parent", featureContext.Values["parent-temp"]);
            featureContext.PermanentValues["permanent"] = "changed-by-child";

            return await featureService.Run(
                new GrandchildRequest(),
                featureContext,
                cancellationToken);
        }
    }

    public sealed class GrandchildFeature : IBaseFeature<GrandchildRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            GrandchildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(featureContext);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            GrandchildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(featureContext);

        private static Task<FeatureResponse<TestResponse>> Handle(IFeatureContext featureContext)
        {
            Assert.False(featureContext.Values.ContainsKey("root-temp"));
            Assert.False(featureContext.Values.ContainsKey("parent-temp"));
            Assert.Equal("changed-by-child", featureContext.Values["permanent"]);
            return Success();
        }
    }

    public sealed class HttpFeature : IBaseFeature<HttpRequest, TestResponse>
    {
        public static HttpContext? SeenHttpContext { get; private set; }

        public Task<FeatureResponse<TestResponse>> HandleClient(
            HttpRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(featureContext);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            HttpRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) => Handle(featureContext);

        private static Task<FeatureResponse<TestResponse>> Handle(IFeatureContext featureContext)
        {
            var httpFeatureContext = Assert.IsAssignableFrom<IHttpFeatureContext>(featureContext);
            SeenHttpContext = httpFeatureContext.HttpContext;
            httpFeatureContext.SetHttpResult(Results.Ok());
            return Success();
        }
    }

    public sealed class ParallelParentFeature(IFeatureService featureService) :
        IBaseFeature<ParallelParentRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            ParallelParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            ParallelParentRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(featureContext, cancellationToken);

        private async Task<FeatureResponse<TestResponse>> Handle(
            IFeatureContext featureContext,
            CancellationToken cancellationToken)
        {
            featureContext.TempValues["shared"] = "shared";
            var responses = await Task.WhenAll(
                featureService.Run(new ParallelChildRequest(1), featureContext, cancellationToken),
                featureService.Run(new ParallelChildRequest(2), featureContext, cancellationToken));
            return responses.All(response => response.Success)
                ? FeatureResponse<TestResponse>.AsSuccess(new())
                : FeatureResponse<TestResponse>.AsFailure();
        }
    }

    public sealed class ParallelChildFeature(
        ScopedDependency scopedDependency,
        ParallelExecutionRecorder recorder) :
        IBaseFeature<ParallelChildRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            ParallelChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(request, featureContext, cancellationToken);

        public Task<FeatureResponse<TestResponse>> HandleServer(
            ParallelChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Handle(request, featureContext, cancellationToken);

        private async Task<FeatureResponse<TestResponse>> Handle(
            ParallelChildRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken)
        {
            await recorder.WaitForBothChildren(cancellationToken);
            featureContext.PermanentValues[$"branch-{request.Branch}"] =
                request.Branch == 1 ? "one" : "two";
            recorder.Observations.Add(new(
                scopedDependency.Id,
                ReferenceEquals(featureContext.FeatureRequest, request),
                Assert.IsType<string>(featureContext.Values["shared"])));
            return FeatureResponse<TestResponse>.AsSuccess(new());
        }
    }

    public sealed class ScopedDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class ParallelExecutionRecorder
    {
        private readonly TaskCompletionSource _bothChildrenStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startedChildren;

        public ConcurrentBag<ParallelObservation> Observations { get; } = [];

        public async Task WaitForBothChildren(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _startedChildren) == 2)
                _bothChildrenStarted.TrySetResult();

            await _bothChildrenStarted.Task.WaitAsync(cancellationToken);
        }
    }

    public sealed record ParallelObservation(
        Guid ScopeId,
        bool RequestMatchesContext,
        string TempValue);

    private static Task<FeatureResponse<TestResponse>> Success() =>
        Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));
}
