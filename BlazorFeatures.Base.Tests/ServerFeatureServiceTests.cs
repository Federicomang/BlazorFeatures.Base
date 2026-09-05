using BlazorFeatures.Abstractions;
using BlazorFeatures.Base.Extensions;
using BlazorFeatures.Base.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Claims;

namespace BlazorFeatures.Base.Tests;

public class ServerFeatureServiceTests
{
    [Fact]
    public void AddFeatures_registers_the_default_server_orchestrator()
    {
        var services = new ServiceCollection();

        services.AddFeatures(features =>
            features.AddAssemblyContaining<ServerFeatureService>());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IServerFeatureService)
            && descriptor.ImplementationType == typeof(ServerFeatureService));
    }

    [Fact]
    public async Task Mandatory_authorization_runs_before_addon_behaviors()
    {
        var behavior = new DenyingMarkerBehavior();
        using var provider = CreateProvider(
            new ClaimsPrincipal(new ClaimsIdentity()),
            behavior);
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            throw new InvalidOperationException("The handler must not run."));

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new AuthorizedMarkerFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, behavior.InvocationCount);
        Assert.Equal(0, handler.InvocationCount);
    }

    [Fact]
    public async Task Addon_behavior_can_short_circuit_a_marked_feature()
    {
        var behavior = new DenyingMarkerBehavior();
        using var provider = CreateProvider(AuthenticatedPrincipal(), behavior);
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new())));

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new MarkerFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, behavior.InvocationCount);
        Assert.Equal(0, handler.InvocationCount);
    }

    [Fact]
    public async Task Behaviors_wrap_the_handler_in_deterministic_order()
    {
        var events = new List<string>();
        using var provider = CreateProvider(
            AuthenticatedPrincipal(),
            new RecordingBehavior(20, "second", events),
            new RecordingBehavior(10, "first", events));
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
        {
            events.Add("handler");
            return Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));
        });

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new PlainFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.True(response.Success);
        Assert.Equal(
            ["before:first", "before:second", "handler", "after:second", "after:first"],
            events);
    }

    [Fact]
    public async Task In_process_exceptions_are_converted_to_a_failure_response()
    {
        using var provider = CreateProvider(AuthenticatedPrincipal());
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            throw new InvalidOperationException("failure"));

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new PlainFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["Internal server error"], response.Messages);
    }

    [Fact]
    public async Task Http_exceptions_are_rethrown_to_the_host_pipeline()
    {
        using var provider = CreateProvider(AuthenticatedPrincipal());
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            throw new InvalidOperationException("failure"));
        var featureContext = new HttpFeatureContext(
            new Microsoft.AspNetCore.Http.DefaultHttpContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider
                .GetRequiredService<IServerFeatureService>()
                .HandleServer(
                    new PlainFeature(),
                    handler,
                    typeof(TestRequest),
                    new TestRequest(),
                    featureContext));
    }

    [Fact]
    public async Task Cancellation_is_never_converted_to_a_failure_response()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var provider = CreateProvider(AuthenticatedPrincipal());
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            Task.FromCanceled<FeatureResponse<TestResponse>>(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider
                .GetRequiredService<IServerFeatureService>()
                .HandleServer(
                    new PlainFeature(),
                    handler,
                    typeof(TestRequest),
                    new TestRequest(),
                    new BaseFeatureContext(),
                    cancellation.Token));
    }

    [Fact]
    public async Task Principal_is_not_resolved_for_a_plain_feature_when_no_behavior_needs_it()
    {
        var principalProvider = new StubPrincipalProvider(AuthenticatedPrincipal());
        using var provider = CreateProvider(principalProvider);
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new())));

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new PlainFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.True(response.Success);
        Assert.Equal(0, principalProvider.InvocationCount);
    }

    [Fact]
    public async Task Principal_is_cached_when_a_behavior_requests_it_more_than_once()
    {
        var principalProvider = new StubPrincipalProvider(AuthenticatedPrincipal());
        using var provider = CreateProvider(
            principalProvider,
            new PrincipalReadingBehavior());
        using var scope = provider.CreateScope();
        var handler = new DelegateHandler<TestResponse>(() =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new())));

        var response = await scope.ServiceProvider
            .GetRequiredService<IServerFeatureService>()
            .HandleServer(
                new PlainFeature(),
                handler,
                typeof(TestRequest),
                new TestRequest(),
                new BaseFeatureContext());

        Assert.True(response.Success);
        Assert.Equal(1, principalProvider.InvocationCount);
    }

    private static ServiceProvider CreateProvider(
        ClaimsPrincipal principal,
        params IServerFeatureBehavior[] behaviors) =>
        CreateProvider(new StubPrincipalProvider(principal), behaviors);

    private static ServiceProvider CreateProvider(
        IFeaturePrincipalProvider principalProvider,
        params IServerFeatureBehavior[] behaviors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddOptions();
        services.AddHttpContextAccessor();
        services.AddSingleton(principalProvider);
        foreach (var behavior in behaviors)
            services.AddSingleton(typeof(IServerFeatureBehavior), behavior);
        services.AddScoped<IServerFeatureService, ServerFeatureService>();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal AuthenticatedPrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], "Test"));

    private sealed class StubPrincipalProvider(ClaimsPrincipal principal) :
        IFeaturePrincipalProvider
    {
        public int InvocationCount { get; private set; }

        public ValueTask<ClaimsPrincipal> GetPrincipalAsync(
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return ValueTask.FromResult(principal);
        }
    }

    private sealed class DelegateHandler<TResponse>(
        Func<Task<FeatureResponse<TResponse>>> handler) : IFeatureHandler<TResponse>
        where TResponse : class
    {
        public int InvocationCount { get; private set; }

        public Task<FeatureResponse<TResponse>> Handle(IFeatureContext featureContext)
        {
            InvocationCount++;
            return handler();
        }
    }

    private interface IRequiresAddonPermission;

    private sealed class DenyingMarkerBehavior : IServerFeatureBehavior
    {
        public int Order => 0;

        public int InvocationCount { get; private set; }

        public Task<FeatureResponse<TResponse>> HandleAsync<TResponse>(
            ServerFeatureExecutionContext<TResponse> context,
            ServerFeatureDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
            where TResponse : class
        {
            InvocationCount++;
            return context.Feature is IRequiresAddonPermission
                ? Task.FromResult(FeatureResponse<TResponse>.AsFailure(
                    statusCode: HttpStatusCode.Forbidden))
                : next();
        }
    }

    private sealed class RecordingBehavior(
        int order,
        string name,
        ICollection<string> events) : IServerFeatureBehavior
    {
        public int Order => order;

        public async Task<FeatureResponse<TResponse>> HandleAsync<TResponse>(
            ServerFeatureExecutionContext<TResponse> context,
            ServerFeatureDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
            where TResponse : class
        {
            events.Add($"before:{name}");
            var response = await next();
            events.Add($"after:{name}");
            return response;
        }
    }

    private sealed class PrincipalReadingBehavior : IServerFeatureBehavior
    {
        public int Order => 0;

        public async Task<FeatureResponse<TResponse>> HandleAsync<TResponse>(
            ServerFeatureExecutionContext<TResponse> context,
            ServerFeatureDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
            where TResponse : class
        {
            var first = await context.GetPrincipalAsync(cancellationToken);
            var second = await context.GetPrincipalAsync(cancellationToken);
            Assert.Same(first, second);
            return await next();
        }
    }

    public sealed class TestRequest : IBaseFeatureRequest<TestResponse>;

    public sealed class TestResponse;

    private abstract class TestFeature : IBaseFeature<TestRequest, TestResponse>
    {
        public Task<FeatureResponse<TestResponse>> HandleClient(
            TestRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));

        public Task<FeatureResponse<TestResponse>> HandleServer(
            TestRequest request,
            IFeatureContext featureContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FeatureResponse<TestResponse>.AsSuccess(new()));
    }

    private sealed class PlainFeature : TestFeature;

    private sealed class MarkerFeature : TestFeature, IRequiresAddonPermission;

    private sealed class AuthorizedMarkerFeature :
        TestFeature,
        IRequiresAddonPermission,
        IBaseFeatureAuthorization
    {
        public void BuildPolicy(AuthorizationPolicyBuilder policy) =>
            policy.RequireAuthenticatedUser();
    }
}
