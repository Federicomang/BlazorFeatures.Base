using BlazorFeatures.Abstractions;
using BlazorFeatures.Abstractions.Enums;
using BlazorFeatures.Abstractions.Tools;
using BlazorFeatures.Base.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace BlazorFeatures.Base
{
    public class FeatureService(IServiceProvider serviceProvider) : IFeatureService
    {
        private static readonly EventId FeatureStartedEvent = new(1000, "FeatureStarted");
        private static readonly EventId FeatureCompletedEvent = new(1001, "FeatureCompleted");
        private static readonly EventId FeatureFailedEvent = new(1002, "FeatureFailed");

        public record EmptyResponse();

        private class ClientHandler<T> : IFeatureHandler<T> where T : class
        {
            private IBaseFeature Feature { get; init; }
            private IBaseFeatureRequest Request { get; init; }
            private CancellationToken CancellationToken { get; init; }

            internal ClientHandler(IBaseFeature feature, IBaseFeatureRequest request, CancellationToken cancellationToken = default)
            {
                Feature = feature;
                Request = request;
                CancellationToken = cancellationToken;
            }

            public async Task<FeatureResponse<T>> Handle(IFeatureContext featureContext)
            {
                var res = await Feature.HandleClient(Request, featureContext, CancellationToken);
                return res.ConvertTo<T>();
            }
        }

        private class ServerHandler<T> : IFeatureHandler<T> where T : class
        {
            private IBaseFeature Feature { get; init; }
            private IBaseFeatureRequest Request { get; init; }
            private CancellationToken CancellationToken { get; init; }

            internal ServerHandler(IBaseFeature feature, IBaseFeatureRequest request, CancellationToken cancellationToken = default)
            {
                Feature = feature;
                Request = request;
                CancellationToken = cancellationToken;
            }

            public async Task<FeatureResponse<T>> Handle(IFeatureContext featureContext)
            {
                var res = await Feature.HandleServer(Request, featureContext, CancellationToken);
                return res.ConvertTo<T>();
            }
        }

        private async Task<FeatureResponse<Response>> Run<Response>(Type requestType, IBaseFeatureRequest<Response> request, IFeatureContext? featureContext, CancellationToken cancellationToken = default) where Response : class
        {
            featureContext ??= new BaseFeatureContext(Constants.IsClientEnvironment
                ? FeatureInvocationSource.Client
                : FeatureInvocationSource.Server);
            featureContext = featureContext.CreateInvocationScope();

            var options = serviceProvider.GetService<IOptions<FeatureTelemetryOptions>>()?.Value
                ?? new FeatureTelemetryOptions();
            var logger = serviceProvider.GetService<ILogger<FeatureService>>()
                ?? NullLogger<FeatureService>.Instance;
            var renderTarget = Constants.IsClientEnvironment
                ? RenderType.Client
                : RenderType.Server;
            var metricTags = new TagList
            {
                { "blazorfeatures.request.type", FormatType(requestType) },
                { "blazorfeatures.response.type", FormatType(typeof(Response)) },
                { "blazorfeatures.invocation_source", featureContext.InvocationSource.ToString() },
                { "blazorfeatures.render_target", renderTarget.ToString() }
            };

            if (options.MetricsEnabled)
                BlazorFeaturesTelemetry.ExecutionCounter.Add(1, in metricTags);

            using var activity = options.TracingEnabled
                ? BlazorFeaturesTelemetry.ActivitySource.StartActivity(
                    $"Feature {requestType.Name}",
                    ActivityKind.Internal)
                : null;
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("blazorfeatures.request.type", FormatType(requestType));
                activity.SetTag("blazorfeatures.response.type", FormatType(typeof(Response)));
                activity.SetTag("blazorfeatures.operation_id", featureContext.OperationId.ToString());
                activity.SetTag("blazorfeatures.invocation_source", featureContext.InvocationSource.ToString());
                activity.SetTag("blazorfeatures.render_target", renderTarget.ToString());
                activity.SetTag("blazorfeatures.chain.length", featureContext.FeatureChain.Count + 1);
            }

            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["FeatureOperationId"] = featureContext.OperationId,
                ["FeatureRequestType"] = FormatType(requestType),
                ["FeatureResponseType"] = FormatType(typeof(Response)),
                ["FeatureInvocationSource"] = featureContext.InvocationSource.ToString(),
                ["FeatureRenderTarget"] = renderTarget.ToString()
            });

            logger.LogDebug(
                FeatureStartedEvent,
                "Executing feature {FeatureRequestType}",
                FormatType(requestType));

            var startedAt = Stopwatch.GetTimestamp();
            var outcome = "error";

            try
            {
                var featureContract = ReflectionTools.GetGenericType(
                    typeof(IBaseFeature<,>),
                    requestType,
                    typeof(Response));
                if (serviceProvider.GetService(featureContract) is not IBaseFeature handler)
                {
                    var featureRegistry = serviceProvider.GetRequiredService<IFeatureRegistry>();
                    var resolution = featureRegistry.Resolve(requestType, typeof(Response));
                    handler = (IBaseFeature)serviceProvider.GetRequiredService(resolution.ImplementationType);
                }

                featureContext.FeatureChain.Add(request);

                activity?.SetTag(
                    "blazorfeatures.implementation.type",
                    FormatType(handler.GetType()));

                FeatureResponse<Response> response;
                if (Constants.IsClientEnvironment)
                {
                    var clientHandler = new ClientHandler<Response>(handler, request, cancellationToken);
                    response = await clientHandler.Handle(featureContext);
                }
                else
                {
                    var serverHandler = new ServerHandler<Response>(handler, request, cancellationToken);
                    var serverService = serviceProvider.GetService<IServerFeatureService>();
                    response = serverService == null
                        ? await serverHandler.Handle(featureContext)
                        : await serverService.HandleServer(handler, serverHandler, requestType, request, featureContext, cancellationToken);
                }

                outcome = response.Success ? "success" : "failure";
                activity?.SetTag("blazorfeatures.success", response.Success);
                if (response.StatusCode.HasValue)
                {
                    activity?.SetTag(
                        "blazorfeatures.http.status_code",
                        (int)response.StatusCode.Value);
                }

                if (!response.Success)
                {
                    activity?.SetStatus(ActivityStatusCode.Error);
                    RecordFailure(options, in metricTags, outcome);
                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }

                logger.LogDebug(
                    FeatureCompletedEvent,
                    "Feature {FeatureRequestType} completed with outcome {FeatureOutcome} and status code {FeatureStatusCode}",
                    FormatType(requestType),
                    outcome,
                    response.StatusCode is null ? null : (int)response.StatusCode);

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outcome = "cancelled";
                activity?.SetTag("blazorfeatures.cancelled", true);
                activity?.SetStatus(ActivityStatusCode.Unset);
                throw;
            }
            catch (Exception exception)
            {
                activity?.SetTag("exception.type", FormatType(exception.GetType()));
                if (options.IncludeExceptionDetails)
                {
                    activity?.SetTag("exception.message", exception.Message);
                    activity?.SetTag("exception.stacktrace", exception.StackTrace);
                }
                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    options.IncludeExceptionDetails ? exception.Message : null);
                RecordFailure(options, in metricTags, outcome);

                if (options.IncludeExceptionDetails)
                {
                    logger.LogError(
                        FeatureFailedEvent,
                        exception,
                        "Feature {FeatureRequestType} failed",
                        FormatType(requestType));
                }
                else
                {
                    logger.LogError(
                        FeatureFailedEvent,
                        "Feature {FeatureRequestType} failed with exception type {FeatureExceptionType}",
                        FormatType(requestType),
                        FormatType(exception.GetType()));
                }
                throw;
            }
            finally
            {
                activity?.SetTag("blazorfeatures.outcome", outcome);
                if (options.MetricsEnabled)
                {
                    var completedTags = metricTags;
                    completedTags.Add("blazorfeatures.outcome", outcome);
                    BlazorFeaturesTelemetry.DurationHistogram.Record(
                        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                        in completedTags);
                }
            }
        }

        private static void RecordFailure(
            FeatureTelemetryOptions options,
            in TagList metricTags,
            string outcome)
        {
            if (!options.MetricsEnabled)
                return;

            var failureTags = metricTags;
            failureTags.Add("blazorfeatures.outcome", outcome);
            BlazorFeaturesTelemetry.FailureCounter.Add(1, in failureTags);
        }

        private static string FormatType(Type type) => type.FullName ?? type.Name;

        public async Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class
        {
            return await Run(request.GetType(), request, null, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>
        {
            return await Run(typeof(Request), request, null, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class
        {
            return await Run(request.GetType(), request, featureContext, cancellationToken);
        }

        public async Task<FeatureResponse<Response>> Run<Request, Response>(IBaseFeatureRequest<Response> request, IFeatureContext featureContext, CancellationToken cancellationToken = default) where Response : class where Request : class, IBaseFeatureRequest<Response>
        {
            return await Run(typeof(Request), request, featureContext, cancellationToken);
        }
    }
}
