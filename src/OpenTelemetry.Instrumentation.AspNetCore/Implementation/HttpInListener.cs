// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
#if !NETSTANDARD
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal class HttpInListener : ListenerHandler
{
    internal const string ActivityOperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    internal const string OnStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    internal const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    internal const string OnUnhandledHostingExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
    internal const string OnUnHandledDiagnosticsExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";

    // https://github.com/dotnet/aspnetcore/blob/8d6554e655b64da75b71e0e20d6db54a3ba8d2fb/src/Hosting/Hosting/src/GenericHost/GenericWebHostBuilder.cs#L85
    internal static readonly string AspNetCoreActivitySourceName = "Microsoft.AspNetCore";

    internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
    internal static readonly bool Net7OrGreater = Environment.Version.Major >= 7;

    private const string DiagnosticSourceName = "Microsoft.AspNetCore";

    private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) =>
    {
        if (request.Headers.TryGetValue(name, out var value))
        {
            // This causes allocation as the `StringValues` struct has to be casted to an `IEnumerable<string>` object.
            return value;
        }

        return Enumerable.Empty<string>();
    };

    private static readonly PropertyFetcher<Exception> ExceptionPropertyFetcher = new("Exception");

    private readonly AspNetCoreTraceInstrumentationOptions options;

    public HttpInListener(AspNetCoreTraceInstrumentationOptions options)
        : base(DiagnosticSourceName)
    {
        Guard.ThrowIfNull(options);

        this.options = options;
    }

    public override void OnEventWritten(string name, object payload)
    {
        switch (name)
        {
            case OnStartEvent:
                {
                    this.OnStartActivity(Activity.Current, payload);
                }

                break;
            case OnStopEvent:
                {
                    this.OnStopActivity(Activity.Current, payload);
                }

                break;
            case OnUnhandledHostingExceptionEvent:
            case OnUnHandledDiagnosticsExceptionEvent:
                {
                    this.OnException(Activity.Current, payload);
                }

                break;
        }
    }

    public void OnStartActivity(Activity activity, object payload)
    {
        // The overall flow of what AspNetCore library does is as below:
        // Activity.Start()
        // DiagnosticSource.WriteEvent("Start", payload)
        // DiagnosticSource.WriteEvent("Stop", payload)
        // Activity.Stop()

        // This method is in the WriteEvent("Start", payload) path.
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        var context = payload as HttpContext;
        if (context == null)
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName);
            return;
        }

        // Ensure context extraction irrespective of sampling decision
        var request = context.Request;
        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        if (textMapPropagator is not TraceContextPropagator)
        {
            var ctx = textMapPropagator.Extract(default, request, HttpRequestHeaderValuesGetter);
            if (ctx.ActivityContext.IsValid()
                && !((ctx.ActivityContext.TraceId == activity.TraceId)
                    && (ctx.ActivityContext.SpanId == activity.ParentSpanId)
                    && (ctx.ActivityContext.TraceState == activity.TraceStateString)))
            {
                // Create a new activity with its parent set from the extracted context.
                // This makes the new activity as a "sibling" of the activity created by
                // Asp.Net Core.
                Activity newOne;
                if (Net7OrGreater)
                {
                    // For NET7.0 onwards activity is created using ActivitySource so,
                    // we will use the source of the activity to create the new one.
                    newOne = activity.Source.CreateActivity(ActivityOperationName, ActivityKind.Server, ctx.ActivityContext);
                }
                else
                {
                    newOne = new Activity(ActivityOperationName);
                    newOne.SetParentId(ctx.ActivityContext.TraceId, ctx.ActivityContext.SpanId, ctx.ActivityContext.TraceFlags);
                }

                newOne.TraceStateString = ctx.ActivityContext.TraceState;

                newOne.SetTag("IsCreatedByInstrumentation", bool.TrueString);

                // Starting the new activity make it the Activity.Current one.
                newOne.Start();

                // Set IsAllDataRequested to false for the activity created by the framework to only export the sibling activity and not the framework activity
                activity.IsAllDataRequested = false;
                activity = newOne;
            }

            Baggage.Current = ctx.Baggage;
        }

        // enrich Activity from payload only if sampling decision
        // is favorable.
        if (activity.IsAllDataRequested)
        {
            try
            {
                if (this.options.Filter?.Invoke(context) == false)
                {
                    AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.RequestFilterException(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName, ex);
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                return;
            }

            if (!Net7OrGreater)
            {
                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Server);
            }

            var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
            activity.DisplayName = GetDisplayName(request.Method);

            // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md

            if (request.Host.HasValue)
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, request.Host.Host);

                if (request.Host.Port.HasValue)
                {
                    activity.SetTag(SemanticConventions.AttributeServerPort, request.Host.Port.Value);
                }
            }

            if (request.QueryString.HasValue)
            {
                // QueryString should be sanitized. see: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4571
                activity.SetTag(SemanticConventions.AttributeUrlQuery, request.QueryString.Value);
            }

            RequestMethodHelper.SetHttpMethodTag(activity, request.Method);

            activity.SetTag(SemanticConventions.AttributeUrlScheme, request.Scheme);
            activity.SetTag(SemanticConventions.AttributeUrlPath, path);
            activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, HttpTagHelper.GetFlavorTagValueFromProtocol(request.Protocol));

            if (request.Headers.TryGetValue("User-Agent", out var values))
            {
                var userAgent = values.Count > 0 ? values[0] : null;
                if (!string.IsNullOrEmpty(userAgent))
                {
                    activity.SetTag(SemanticConventions.AttributeUserAgentOriginal, userAgent);
                }
            }

            try
            {
                this.options.EnrichWithHttpRequest?.Invoke(activity, request);
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName, ex);
            }
        }
    }

    public void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            HttpContext context = payload as HttpContext;
            if (context == null)
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStopActivity), activity.OperationName);
                return;
            }

            var response = context.Response;

#if !NETSTANDARD
            var routePattern = (context.Features.Get<IExceptionHandlerPathFeature>()?.Endpoint as RouteEndpoint ??
                    context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            if (!string.IsNullOrEmpty(routePattern))
            {
                activity.DisplayName = GetDisplayName(context.Request.Method, routePattern);
                activity.SetTag(SemanticConventions.AttributeHttpRoute, routePattern);
            }
#endif

            activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(response.StatusCode));

            if (this.options.EnableGrpcAspNetCoreSupport && TryGetGrpcMethod(activity, out var grpcMethod))
            {
                AddGrpcAttributes(activity, grpcMethod, context);
            }

            if (activity.Status == ActivityStatusCode.Unset)
            {
                activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, response.StatusCode));
            }

            try
            {
                this.options.EnrichWithHttpResponse?.Invoke(activity, response);
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnStopActivity), activity.OperationName, ex);
            }
        }

        object tagValue;
        if (Net7OrGreater)
        {
            tagValue = activity.GetTagValue("IsCreatedByInstrumentation");
        }
        else
        {
            _ = activity.TryCheckFirstTag("IsCreatedByInstrumentation", out tagValue);
        }

        if (ReferenceEquals(tagValue, bool.TrueString))
        {
            // If instrumentation started a new Activity, it must
            // be stopped here.
            activity.SetTag("IsCreatedByInstrumentation", null);
            activity.Stop();

            // After the activity.Stop() code, Activity.Current becomes null.
            // If Asp.Net Core uses Activity.Current?.Stop() - it'll not stop the activity
            // it created.
            // Currently Asp.Net core does not use Activity.Current, instead it stores a
            // reference to its activity, and calls .Stop on it.

            // TODO: Should we still restore Activity.Current here?
            // If yes, then we need to store the asp.net core activity inside
            // the one created by the instrumentation.
            // And retrieve it here, and set it to Current.
        }
    }

    public void OnException(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            // We need to use reflection here as the payload type is not a defined public type.
            if (!TryFetchException(payload, out Exception exc))
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnException), activity.OperationName);
                return;
            }

            activity.SetTag(SemanticConventions.AttributeErrorType, exc.GetType().FullName);

            if (this.options.RecordException)
            {
                activity.RecordException(exc);
            }

            activity.SetStatus(ActivityStatusCode.Error);

            try
            {
                this.options.EnrichWithException?.Invoke(activity, exc);
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnException), activity.OperationName, ex);
            }
        }

        // See https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L252
        // and https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddlewareImpl.cs#L174
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchException(object payload, out Exception exc)
            => ExceptionPropertyFetcher.TryFetch(payload, out exc) && exc != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetGrpcMethod(Activity activity, out string grpcMethod)
    {
        grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);
        return !string.IsNullOrEmpty(grpcMethod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddGrpcAttributes(Activity activity, string grpcMethod, HttpContext context)
    {
        // The RPC semantic conventions indicate the span name
        // should not have a leading forward slash.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md#span-name
        activity.DisplayName = grpcMethod.TrimStart('/');

        activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

        // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/rpc/rpc-spans.md

        if (context.Connection.RemoteIpAddress != null)
        {
            activity.SetTag(SemanticConventions.AttributeClientAddress, context.Connection.RemoteIpAddress.ToString());
        }

        activity.SetTag(SemanticConventions.AttributeClientPort, context.Connection.RemotePort);

        bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
        if (validConversion)
        {
            activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status));
        }

        if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
        {
            activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
            activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

            // Remove the grpc.method tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);

            // Remove the grpc.status_code tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

            if (validConversion)
            {
                // setting rpc.grpc.status_code
                activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);
            }
        }
    }

    private static string GetDisplayName(string httpMethod, string httpRoute = null)
    {
        var normalizedMethod = RequestMethodHelper.GetNormalizedHttpMethod(httpMethod);

        return string.IsNullOrEmpty(httpRoute)
            ? normalizedMethod
            : $"{normalizedMethod} {httpRoute}";
    }
}
