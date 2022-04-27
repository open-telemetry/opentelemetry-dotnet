// <copyright file="HttpHandlerDiagnosticListener.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Implementation
{
    internal sealed class HttpHandlerDiagnosticListener : ListenerHandler
    {
        internal static readonly AssemblyName AssemblyName = typeof(HttpHandlerDiagnosticListener).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

        private static readonly Regex CoreAppMajorVersionCheckRegex = new("^\\.NETCoreApp,Version=v(\\d+)\\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly PropertyFetcher<HttpRequestMessage> startRequestFetcher = new("Request");
        private readonly PropertyFetcher<HttpResponseMessage> stopResponseFetcher = new("Response");
        private readonly PropertyFetcher<Exception> stopExceptionFetcher = new("Exception");
        private readonly PropertyFetcher<TaskStatus> stopRequestStatusFetcher = new("RequestTaskStatus");
        private readonly HttpClientInstrumentationOptions options;

        public HttpHandlerDiagnosticListener(HttpClientInstrumentationOptions options)
            : base("HttpHandlerDiagnosticListener")
        {
            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            // The overall flow of what HttpClient library does is as below:
            // Activity.Start()
            // DiagnosticSource.WriteEvent("Start", payload)
            // DiagnosticSource.WriteEvent("Stop", payload)
            // Activity.Stop()

            // This method is in the WriteEvent("Start", payload) path.
            // By this time, samplers have already run and
            // activity.IsAllDataRequested populated accordingly.

            if (Sdk.SuppressInstrumentation)
            {
                return;
            }

            if (!this.startRequestFetcher.TryFetch(payload, out HttpRequestMessage request) || request == null)
            {
                HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            // TODO: Investigate why this check is needed.
            if (Propagators.DefaultTextMapPropagator.Extract(default, request, HttpRequestMessageContextPropagation.HeaderValuesGetter) != default)
            {
                // this request is already instrumented, we should back off
                activity.IsAllDataRequested = false;
                return;
            }

            // Propagate context irrespective of sampling decision
            var textMapPropagator = Propagators.DefaultTextMapPropagator;
            if (textMapPropagator is not TraceContextPropagator)
            {
                textMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), request, HttpRequestMessageContextPropagation.HeaderValueSetter);
            }

            // enrich Activity from payload only if sampling decision
            // is favorable.
            if (activity.IsAllDataRequested)
            {
                try
                {
                    if (this.options.EventFilter(activity.OperationName, request) == false)
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

                activity.DisplayName = HttpTagHelper.GetOperationNameForHttpMethod(request.Method);

                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

                activity.SetTag(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method));
                activity.SetTag(SemanticConventions.AttributeHttpHost, HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                activity.SetTag(SemanticConventions.AttributeHttpUrl, HttpTagHelper.GetUriTagValueFromRequestUri(request.RequestUri));
                if (this.options.SetHttpFlavor)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version));
                }

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    HttpInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
                // requestTaskStatus is not null
                _ = this.stopRequestStatusFetcher.TryFetch(payload, out var requestTaskStatus);

                StatusCode currentStatusCode = activity.GetStatus().StatusCode;
                if (requestTaskStatus != TaskStatus.RanToCompletion)
                {
                    if (requestTaskStatus == TaskStatus.Canceled)
                    {
                        if (currentStatusCode == StatusCode.Unset)
                        {
                            activity.SetStatus(Status.Error);
                        }
                    }
                    else if (requestTaskStatus != TaskStatus.Faulted)
                    {
                        if (currentStatusCode == StatusCode.Unset)
                        {
                            // Faults are handled in OnException and should already have a span.Status of Error w/ Description.
                            activity.SetStatus(Status.Error);
                        }
                    }
                }

                if (this.stopResponseFetcher.TryFetch(payload, out HttpResponseMessage response) && response != null)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode);

                    if (currentStatusCode == StatusCode.Unset)
                    {
                        activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, (int)response.StatusCode));
                    }

                    try
                    {
                        this.options.Enrich?.Invoke(activity, "OnStopActivity", response);
                    }
                    catch (Exception ex)
                    {
                        HttpInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                if (!this.stopExceptionFetcher.TryFetch(payload, out Exception exc) || exc == null)
                {
                    HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnException));
                    return;
                }

                if (this.options.RecordException)
                {
                    activity.RecordException(exc);
                }

                if (exc is HttpRequestException)
                {
                    if (exc.InnerException is SocketException exception)
                    {
                        switch (exception.SocketErrorCode)
                        {
                            case SocketError.HostNotFound:
                                activity.SetStatus(Status.Error.WithDescription(exc.Message));
                                return;
                        }
                    }

                    if (exc.InnerException != null)
                    {
                        activity.SetStatus(Status.Error.WithDescription(exc.Message));
                    }
                }

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnException", exc);
                }
                catch (Exception ex)
                {
                    HttpInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }
    }
}
