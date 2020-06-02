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
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class HttpHandlerDiagnosticListener : ListenerHandler
    {
        private static readonly Regex CoreAppMajorVersionCheckRegex = new Regex("^\\.NETCoreApp,Version=v(\\d+)\\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");
        private readonly bool httpClientSupportsW3C = false;
        private readonly HttpClientInstrumentationOptions options;

        public HttpHandlerDiagnosticListener(Tracer tracer, HttpClientInstrumentationOptions options)
            : base("HttpHandlerDiagnosticListener", tracer)
        {
            var framework = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            // Depending on the .NET version/flavor this will look like
            // '.NETCoreApp,Version=v3.0', '.NETCoreApp,Version = v2.2' or '.NETFramework,Version = v4.7.1'

            if (framework != null)
            {
                var match = CoreAppMajorVersionCheckRegex.Match(framework);

                this.httpClientSupportsW3C = match.Success && int.Parse(match.Groups[1].Value) >= 3;
            }

            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                InstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                return;
            }

            if (request.Headers.Contains("traceparent"))
            {
                // this request is already instrumented, we should back off
                return;
            }

            this.Tracer.StartActiveSpanFromActivity(HttpTagHelper.GetOperationNameForHttpMethod(request.Method), activity, SpanKind.Client, out var span);

            if (span.IsRecording)
            {
                span.PutComponentAttribute("http");
                span.PutHttpMethodAttribute(HttpTagHelper.GetNameForHttpMethod(request.Method));
                span.PutHttpHostAttribute(HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                span.PutHttpRawUrlAttribute(request.RequestUri.OriginalString);

                if (this.options.SetHttpFlavor)
                {
                    span.PutHttpFlavorAttribute(HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version));
                }
            }

            if (!(this.httpClientSupportsW3C && this.options.TextFormat is TraceContextFormat))
            {
                this.options.TextFormat.Inject(span.Context, request, (r, k, v) => r.Headers.Add(k, v));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;
            try
            {
                if (span == null || !span.Context.IsValid)
                {
                    InstrumentationEventSource.Log.NullOrBlankSpan(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (span.IsRecording)
                {
                    var requestTaskStatus = this.stopRequestStatusFetcher.Fetch(payload) as TaskStatus?;

                    if (requestTaskStatus.HasValue)
                    {
                        if (requestTaskStatus != TaskStatus.RanToCompletion)
                        {
                            if (requestTaskStatus == TaskStatus.Canceled)
                            {
                                span.Status = Status.Cancelled;
                            }
                            else if (requestTaskStatus != TaskStatus.Faulted)
                            {
                                // Faults are handled in OnException and should already have a span.Status of Unknown w/ Description.
                                span.Status = Status.Unknown;
                            }
                        }
                    }

                    if (this.stopResponseFetcher.Fetch(payload) is HttpResponseMessage response)
                    {
                        // response could be null for DNS issues, timeouts, etc...
                        span.PutHttpStatusCode((int)response.StatusCode, response.ReasonPhrase);
                    }
                }
            }
            finally
            {
                span?.End();
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnException";
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                InstrumentationEventSource.Log.NullOrBlankSpan(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                return;
            }

            if (span.IsRecording)
            {
                if (!(this.stopExceptionFetcher.Fetch(payload) is Exception exc))
                {
                    InstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (exc is HttpRequestException)
                {
                    if (exc.InnerException is SocketException exception)
                    {
                        switch (exception.SocketErrorCode)
                        {
                            case SocketError.HostNotFound:
                                span.Status = Status.InvalidArgument.WithDescription(exc.Message);
                                return;
                        }
                    }

                    if (exc.InnerException != null)
                    {
                        span.Status = Status.Unknown.WithDescription(exc.Message);
                    }
                }
            }

            // Note: Span.End() is not called here on purpose, OnStopActivity is called after OnException for this listener.
        }
    }
}
