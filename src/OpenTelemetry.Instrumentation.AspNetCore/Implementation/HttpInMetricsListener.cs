/// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Trace;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal sealed class HttpInMetricsListener : ListenerHandler
{
    internal const string HttpServerDurationMetricName = "http.server.duration";
    internal const string HttpServerRequestDurationMetricName = "http.server.request.duration";

    private const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    private const string EventName = "OnStopActivity";

    private readonly Meter meter;
    private readonly AspNetCoreMetricsInstrumentationOptions options;
    private readonly Histogram<double> httpServerDuration;
    private readonly Histogram<double> httpServerRequestDuration;
    private readonly bool emitOldAttributes;
    private readonly bool emitNewAttributes;

    internal HttpInMetricsListener(string name, Meter meter, AspNetCoreMetricsInstrumentationOptions options)
        : base(name)
    {
        this.meter = meter;
        this.options = options;

        this.emitOldAttributes = this.options.HttpSemanticConvention.HasFlag(HttpSemanticConvention.Old);

        this.emitNewAttributes = this.options.HttpSemanticConvention.HasFlag(HttpSemanticConvention.New);

        if (this.emitOldAttributes)
        {
            this.httpServerDuration = meter.CreateHistogram<double>(HttpServerDurationMetricName, "ms", "Measures the duration of inbound HTTP requests.");
        }

        if (this.emitNewAttributes)
        {
            this.httpServerRequestDuration = meter.CreateHistogram<double>(HttpServerRequestDurationMetricName, "s", "Measures the duration of inbound HTTP requests.");
        }
    }

    public override void OnEventWritten(string name, object payload)
    {
        if (name == OnStopEvent)
        {
            if (this.emitOldAttributes)
            {
                this.OnEventWritten_Old(name, payload);
            }

            if (this.emitNewAttributes)
            {
                this.OnEventWritten_New(name, payload);
            }
        }
    }

    public void OnEventWritten_Old(string name, object payload)
    {
        var context = payload as HttpContext;
        if (context == null)
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), EventName, HttpServerDurationMetricName);
            return;
        }

        try
        {
            if (this.options.Filter?.Invoke(HttpServerDurationMetricName, context) == false)
            {
                AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(nameof(HttpInMetricsListener), EventName, HttpServerDurationMetricName);
                return;
            }
        }
        catch (Exception ex)
        {
            AspNetCoreInstrumentationEventSource.Log.RequestFilterException(nameof(HttpInMetricsListener), EventName, HttpServerDurationMetricName, ex);
            return;
        }

        // TODO: Prometheus pulls metrics by invoking the /metrics endpoint. Decide if it makes sense to suppress this.
        // Below is just a temporary way of achieving this suppression for metrics (we should consider suppressing traces too).
        // If we want to suppress activity from Prometheus then we should use SuppressInstrumentationScope.
        if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("metrics"))
        {
            return;
        }

        TagList tags = default;

        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol)));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, context.Request.Scheme));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, context.Request.Method));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, TelemetryHelper.GetBoxedStatusCode(context.Response.StatusCode)));

        if (context.Request.Host.HasValue)
        {
            tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, context.Request.Host.Host));

            if (context.Request.Host.Port is not null && context.Request.Host.Port != 80 && context.Request.Host.Port != 443)
            {
                tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostPort, context.Request.Host.Port));
            }
        }

#if NET6_0_OR_GREATER
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrEmpty(route))
        {
            tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, route));
        }
#endif
        if (this.options.Enrich != null)
        {
            try
            {
                this.options.Enrich(HttpServerDurationMetricName, context, ref tags);
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInMetricsListener), EventName, HttpServerDurationMetricName, ex);
            }
        }

        // We are relying here on ASP.NET Core to set duration before writing the stop event.
        // https://github.com/dotnet/aspnetcore/blob/d6fa351048617ae1c8b47493ba1abbe94c3a24cf/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L449
        // TODO: Follow up with .NET team if we can continue to rely on this behavior.
        this.httpServerDuration.Record(Activity.Current.Duration.TotalMilliseconds, tags);
    }

    public void OnEventWritten_New(string name, object payload)
    {
        var context = payload as HttpContext;
        if (context == null)
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), EventName, HttpServerRequestDurationMetricName);
            return;
        }

        try
        {
            if (this.options.Filter?.Invoke(HttpServerRequestDurationMetricName, context) == false)
            {
                AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(nameof(HttpInMetricsListener), EventName, HttpServerRequestDurationMetricName);
                return;
            }
        }
        catch (Exception ex)
        {
            AspNetCoreInstrumentationEventSource.Log.RequestFilterException(nameof(HttpInMetricsListener), EventName, HttpServerRequestDurationMetricName, ex);
            return;
        }

        // TODO: Prometheus pulls metrics by invoking the /metrics endpoint. Decide if it makes sense to suppress this.
        // Below is just a temporary way of achieving this suppression for metrics (we should consider suppressing traces too).
        // If we want to suppress activity from Prometheus then we should use SuppressInstrumentationScope.
        if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("metrics"))
        {
            return;
        }

        TagList tags = default;

        // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-spans.md
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetworkProtocolVersion, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol)));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeUrlScheme, context.Request.Scheme));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRequestMethod, context.Request.Method));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(context.Response.StatusCode)));

#if NET6_0_OR_GREATER
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrEmpty(route))
        {
            tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, route));
        }
#endif
        if (this.options.Enrich != null)
        {
            try
            {
                this.options.Enrich(HttpServerRequestDurationMetricName, context, ref tags);
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInMetricsListener), EventName, HttpServerRequestDurationMetricName, ex);
            }
        }

        // We are relying here on ASP.NET Core to set duration before writing the stop event.
        // https://github.com/dotnet/aspnetcore/blob/d6fa351048617ae1c8b47493ba1abbe94c3a24cf/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L449
        // TODO: Follow up with .NET team if we can continue to rely on this behavior.
        this.httpServerRequestDuration.Record(Activity.Current.Duration.TotalSeconds, tags);
    }
}
