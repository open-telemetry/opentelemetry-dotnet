// <copyright file="HttpInMetricsListener.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Internal;

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal sealed class HttpInMetricsListener : ListenerHandler
{
    internal const string HttpServerRequestDurationMetricName = "http.server.request.duration";

    internal const string OnUnhandledHostingExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
    internal const string OnUnhandledDiagnosticsExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";

    internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();
    internal static readonly Meter Meter = new(InstrumentationName, InstrumentationVersion);

    private const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";

    private const string EventName = "OnStopActivity";
    private const string NetworkProtocolName = "http";
    private static readonly PropertyFetcher<Exception> ExceptionPropertyFetcher = new("Exception");
    private static readonly PropertyFetcher<HttpContext> HttpContextPropertyFetcher = new("HttpContext");
    private static readonly object ErrorTypeHttpContextItemsKey = new();

    private static readonly Histogram<double> HttpServerRequestDuration = Meter.CreateHistogram<double>(HttpServerRequestDurationMetricName, "s", "Duration of HTTP server requests.");

    internal HttpInMetricsListener(string name)
        : base(name)
    {
    }

    public override void OnEventWritten(string name, object payload)
    {
        switch (name)
        {
            case OnUnhandledDiagnosticsExceptionEvent:
            case OnUnhandledHostingExceptionEvent:
                {
                    this.OnExceptionEventWritten(name, payload);
                }

                break;
            case OnStopEvent:
                {
                    this.OnStopEventWritten(name, payload);
                }

                break;
        }
    }

    public void OnExceptionEventWritten(string name, object payload)
    {
        // We need to use reflection here as the payload type is not a defined public type.
        if (!TryFetchException(payload, out Exception exc) || !TryFetchHttpContext(payload, out HttpContext ctx))
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), nameof(this.OnExceptionEventWritten), HttpServerRequestDurationMetricName);
            return;
        }

        ctx.Items.Add(ErrorTypeHttpContextItemsKey, exc.GetType().FullName);

        // See https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L252
        // and https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddlewareImpl.cs#L174
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ASP.NET Core framework guarantees that top level properties are preserved")]
#endif
        static bool TryFetchException(object payload, out Exception exc)
            => ExceptionPropertyFetcher.TryFetch(payload, out exc) && exc != null;
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The ASP.NET Core framework guarantees that top level properties are preserved")]
#endif
        static bool TryFetchHttpContext(object payload, out HttpContext ctx)
            => HttpContextPropertyFetcher.TryFetch(payload, out ctx) && ctx != null;
    }

    public void OnStopEventWritten(string name, object payload)
    {
        var context = payload as HttpContext;
        if (context == null)
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), EventName, HttpServerRequestDurationMetricName);
            return;
        }

        TagList tags = default;

        // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-spans.md
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetworkProtocolVersion, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol)));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeUrlScheme, context.Request.Scheme));
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(context.Response.StatusCode)));

        var httpMethod = RequestMethodHelper.GetNormalizedHttpMethod(context.Request.Method);
        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRequestMethod, httpMethod));

#if NET6_0_OR_GREATER
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrEmpty(route))
        {
            tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, route));
        }
#endif
        if (context.Items.TryGetValue(ErrorTypeHttpContextItemsKey, out var errorType))
        {
            tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeErrorType, errorType));
        }

        // We are relying here on ASP.NET Core to set duration before writing the stop event.
        // https://github.com/dotnet/aspnetcore/blob/d6fa351048617ae1c8b47493ba1abbe94c3a24cf/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L449
        // TODO: Follow up with .NET team if we can continue to rely on this behavior.
        HttpServerRequestDuration.Record(Activity.Current.Duration.TotalSeconds, tags);
    }
}
