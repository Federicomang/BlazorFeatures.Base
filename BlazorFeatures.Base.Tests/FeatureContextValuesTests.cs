using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorFeatures.Base.Tests;

public class FeatureContextValuesTests
{
    [Fact]
    public void Values_combines_permanent_and_temporary_values_with_temporary_precedence()
    {
        var context = new BaseFeatureContext();
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
    public async Task Temporary_values_reach_only_the_next_direct_feature_invocation()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = new BaseFeatureContext();
        context.TempValues["root-temp"] = "root";
        context.PermanentValues["permanent"] = "initial";

        var response = await scope.ServiceProvider
            .GetRequiredService<IFeatureService>()
            .Run(new ParentRequest(), context);

        Assert.True(response.Success);
        Assert.Empty(context.TempValues);
        Assert.Equal("changed-by-child", context.PermanentValues["permanent"]);
    }

    [Fact]
    public async Task Http_context_is_preserved_in_invocation_scopes()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var httpContext = new DefaultHttpContext();
        var context = new HttpFeatureContext(httpContext);
        var request = new HttpRequest();

        var response = await scope.ServiceProvider
            .GetRequiredService<IFeatureService>()
            .Run(request, context);

        Assert.True(response.Success);
        Assert.Same(httpContext, HttpFeature.SeenHttpContext);
        Assert.True(context.TryGetHttpResult(request, out _));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IFeatureService, FeatureService>();
        services.AddScoped<IBaseFeature<ParentRequest, TestResponse>, ParentFeature>();
        services.AddScoped<IBaseFeature<ChildRequest, TestResponse>, ChildFeature>();
        services.AddScoped<IBaseFeature<GrandchildRequest, TestResponse>, GrandchildFeature>();
        services.AddScoped<IBaseFeature<HttpRequest, TestResponse>, HttpFeature>();
        return services.BuildServiceProvider();
    }

    public sealed class ParentRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class ChildRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class GrandchildRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class HttpRequest : IBaseFeatureRequest<TestResponse>;

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
            Assert.False(featureContext.Values.ContainsKey("parent-temp"));
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
            httpFeatureContext.SetHttpResult(
                Assert.IsType<HttpRequest>(featureContext.FeatureChain.Last()),
                Results.Ok());
            return Success();
        }
    }

    private static Task<FeatureResponse<TestResponse>> Success() =>
        Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));
}
