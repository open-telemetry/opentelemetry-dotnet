// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal sealed class HttpHandlerDiagnosticListener(
    HttpClientTraceInstrumentationOptions options,
    RequestMethodHelper requestMethodHelper) : ListenerHandler("HttpHandlerDiagnosticListener")
{
    internal static readonly AssemblyName AssemblyName = typeof(HttpHandlerDiagnosticListener).Assembly.GetName();
    internal static readonly bool IsNet7OrGreater;

    // https://github.com/dotnet/runtime/blob/7d034ddbbbe1f2f40c264b323b3ed3d6b3d45e9a/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L19
    internal static readonly string HttpClientActivitySourceName = "System.Net.Http";
    internal static readonly string ActivitySourceName = AssemblyName.Name + ".HttpClient";
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

    private const string OnStartEvent = "System.Net.Http.HttpRequestOut.Start";
    private const string OnStopEvent = "System.Net.Http.HttpRequestOut.Stop";
    private const string OnUnhandledExceptionEvent = "System.Net.Http.Exception";

    private static readonly PropertyFetcher<HttpRequestMessage> StartRequestFetcher = new("Request");
    private static readonly PropertyFetcher<HttpResponseMessage> StopResponseFetcher = new("Response");
    private static readonly PropertyFetcher<Exception> StopExceptionFetcher = new("Exception");
    private static readonly PropertyFetcher<TaskStatus> StopRequestStatusFetcher = new("RequestTaskStatus");
    private readonly HttpClientTraceInstrumentationOptions options = options;
    private readonly RequestMethodHelper requestMethodHelper = requestMethodHelper;

    static HttpHandlerDiagnosticListener()
    {
        try
        {
            IsNet7OrGreater = typeof(HttpClient).Assembly.GetName().Version.Major >= 7;
        }
        catch (Exception)
        {
            IsNet7OrGreater = false;
        }
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
            case OnUnhandledExceptionEvent:
                {
                    this.OnException(Activity.Current, payload);
                }

                break;
        }
    }

    public void OnStartActivity(Activity activity, object payload)
    {
        // The overall flow of what HttpClient library does is as below:
        // Activity.Start()
        // DiagnosticSource.WriteEvent("Start", payload)
        // DiagnosticSource.WriteEvent("Stop", payload)
        // Activity.Stop()

        // This method is in the WriteEvent("Start", payload) path.
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (!TryFetchRequest(payload, out HttpRequestMessage request))
        {
            HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnStartActivity));
            return;
        }

        // Propagate context irrespective of sampling decision
        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        if (textMapPropagator is not TraceContextPropagator)
        {
            textMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), request, HttpRequestMessageContextPropagation.HeaderValueSetter);
        }

        // For .NET7.0 or higher versions, activity is created using activity source.
        // However the framework will fallback to creating activity if the sampler's decision is to drop and there is a active diagnostic listener.
        // To prevent processing such activities we first check the source name to confirm if it was created using
        // activity source or not.
        if (IsNet7OrGreater && string.IsNullOrEmpty(activity.Source.Name))
        {
            activity.IsAllDataRequested = false;
        }

        // enrich Activity from payload only if sampling decision
        // is favorable.
        if (activity.IsAllDataRequested)
        {
            try
            {
                if (this.options.EventFilterHttpRequestMessage(activity.OperationName, request) == false)
                {
                    HttpInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                return;
            }

            this.requestMethodHelper.SetHttpClientActivityDisplayName(activity, request.Method.Method);

            if (!IsNet7OrGreater)
            {
                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);
            }

            // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md
            this.requestMethodHelper.SetHttpMethodTag(activity, request.Method.Method);

            activity.SetTag(SemanticConventions.AttributeServerAddress, request.RequestUri.Host);
            if (!request.RequestUri.IsDefaultPort)
            {
                activity.SetTag(SemanticConventions.AttributeServerPort, request.RequestUri.Port);
            }

            activity.SetTag(SemanticConventions.AttributeUrlFull, HttpTagHelper.GetUriTagValueFromRequestUri(request.RequestUri));

            try
            {
                this.options.EnrichWithHttpRequestMessage?.Invoke(activity, request);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }

        // The AOT-annotation DynamicallyAccessedMembers in System.Net.Http library ensures that top-level properties on the payload object are always preserved.
        // see https://github.com/dotnet/runtime/blob/f9246538e3d49b90b0e9128d7b1defef57cd6911/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L325
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top-level properties are preserved")]
#endif
        static bool TryFetchRequest(object payload, out HttpRequestMessage request)
        {
            if (!StartRequestFetcher.TryFetch(payload, out request) || request == null)
            {
                return false;
            }

            return true;
        }
    }

    public void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            var requestTaskStatus = GetRequestStatus(payload);

            ActivityStatusCode currentStatusCode = activity.Status;
            if (requestTaskStatus != TaskStatus.RanToCompletion)
            {
                if (requestTaskStatus == TaskStatus.Canceled)
                {
                    if (currentStatusCode == ActivityStatusCode.Unset)
                    {
                        activity.SetStatus(ActivityStatusCode.Error);
                    }
                }
                else if (requestTaskStatus != TaskStatus.Faulted)
                {
                    if (currentStatusCode == ActivityStatusCode.Unset)
                    {
                        // Faults are handled in OnException and should already have a span.Status of Error w/ Description.
                        activity.SetStatus(ActivityStatusCode.Error);
                    }
                }
            }

            if (TryFetchResponse(payload, out HttpResponseMessage response))
            {
                if (currentStatusCode == ActivityStatusCode.Unset)
                {
                    activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, (int)response.StatusCode));
                }

                activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, HttpTagHelper.GetProtocolVersionString(response.Version));
                activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(response.StatusCode));
                if (activity.Status == ActivityStatusCode.Error)
                {
                    activity.SetTag(SemanticConventions.AttributeErrorType, TelemetryHelper.GetStatusCodeString(response.StatusCode));
                }

                try
                {
                    this.options.EnrichWithHttpResponseMessage?.Invoke(activity, response);
                }
                catch (Exception ex)
                {
                    HttpInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            // The AOT-annotation DynamicallyAccessedMembers in System.Net.Http library ensures that top-level properties on the payload object are always preserved.
            // see https://github.com/dotnet/runtime/blob/f9246538e3d49b90b0e9128d7b1defef57cd6911/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L325
#if NET6_0_OR_GREATER
            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top-level properties are preserved")]
#endif
            static TaskStatus GetRequestStatus(object payload)
            {
                // requestTaskStatus (type is TaskStatus) is a non-nullable enum so we don't need to have a null check here.
                // See: https://github.com/dotnet/runtime/blob/79c021d65c280020246d1035b0e87ae36f2d36a9/src/libraries/System.Net.Http/src/HttpDiagnosticsGuide.md?plain=1#L69
                _ = StopRequestStatusFetcher.TryFetch(payload, out var requestTaskStatus);

                return requestTaskStatus;
            }
        }

        // The AOT-annotation DynamicallyAccessedMembers in System.Net.Http library ensures that top-level properties on the payload object are always preserved.
        // see https://github.com/dotnet/runtime/blob/f9246538e3d49b90b0e9128d7b1defef57cd6911/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L325
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top-level properties are preserved")]
#endif
        static bool TryFetchResponse(object payload, out HttpResponseMessage response)
        {
            if (StopResponseFetcher.TryFetch(payload, out response) && response != null)
            {
                return true;
            }

            return false;
        }
    }

    public void OnException(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            if (!TryFetchException(payload, out Exception exc))
            {
                HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnException));
                return;
            }

            activity.SetTag(SemanticConventions.AttributeErrorType, GetErrorType(exc));

            if (this.options.RecordException)
            {
                activity.RecordException(exc);
            }

            if (exc is HttpRequestException)
            {
                activity.SetStatus(ActivityStatusCode.Error);
            }

            try
            {
                this.options.EnrichWithException?.Invoke(activity, exc);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }

        // The AOT-annotation DynamicallyAccessedMembers in System.Net.Http library ensures that top-level properties on the payload object are always preserved.
        // see https://github.com/dotnet/runtime/blob/f9246538e3d49b90b0e9128d7b1defef57cd6911/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L325
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top-level properties are preserved")]
#endif
        static bool TryFetchException(object payload, out Exception exc)
        {
            if (!StopExceptionFetcher.TryFetch(payload, out exc) || exc == null)
            {
                return false;
            }

            return true;
        }
    }

    private static string GetErrorType(Exception exc)
    {
#if NET8_0_OR_GREATER
        // For net8.0 and above exception type can be found using HttpRequestError.
        // https://learn.microsoft.com/dotnet/api/system.net.http.httprequesterror?view=net-8.0
        if (exc is HttpRequestException httpRequestException)
        {
            return httpRequestException.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "name_resolution_error",
                HttpRequestError.ConnectionError => "connection_error",
                HttpRequestError.SecureConnectionError => "secure_connection_error",
                HttpRequestError.HttpProtocolError => "http_protocol_error",
                HttpRequestError.ExtendedConnectNotSupported => "extended_connect_not_supported",
                HttpRequestError.VersionNegotiationError => "version_negotiation_error",
                HttpRequestError.UserAuthenticationError => "user_authentication_error",
                HttpRequestError.ProxyTunnelError => "proxy_tunnel_error",
                HttpRequestError.InvalidResponse => "invalid_response",
                HttpRequestError.ResponseEnded => "response_ended",
                HttpRequestError.ConfigurationLimitExceeded => "configuration_limit_exceeded",

                // Fall back to the exception type name in case of HttpRequestError.Unknown
                _ => exc.GetType().FullName,
            };
        }
#endif
        return exc.GetType().FullName;
    }
}
