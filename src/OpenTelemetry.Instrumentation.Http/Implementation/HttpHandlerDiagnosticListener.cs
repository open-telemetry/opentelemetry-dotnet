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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Implementation
{
    internal class HttpHandlerDiagnosticListener : ListenerHandler
    {
        public const string RequestCustomPropertyName = "OTel.HttpHandler.Request";
        public const string ResponseCustomPropertyName = "OTel.HttpHandler.Response";
        public const string ExceptionCustomPropertyName = "OTel.HttpHandler.Exception";

        private static readonly Func<HttpRequestMessage, string, IEnumerable<string>> HttpRequestMessageHeaderValuesGetter = (request, name) =>
        {
            if (request.Headers.TryGetValues(name, out var values))
            {
                return values;
            }

            return null;
        };

        private static readonly Action<HttpRequestMessage, string, string> HttpRequestMessageHeaderValueSetter = (request, name, value) => request.Headers.Add(name, value);

        private static readonly Regex CoreAppMajorVersionCheckRegex = new Regex("^\\.NETCoreApp,Version=v(\\d+)\\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");
        private readonly bool httpClientSupportsW3C;
        private readonly HttpClientInstrumentationOptions options;

        public HttpHandlerDiagnosticListener(HttpClientInstrumentationOptions options, ActivitySourceAdapter activitySource)
            : base("HttpHandlerDiagnosticListener")
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
            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            if (this.options.Propagator.Extract(default, request, HttpRequestMessageHeaderValuesGetter) != default)
            {
                // this request is already instrumented, we should back off
                activity.IsAllDataRequested = false;
                return;
            }

            activity.DisplayName = HttpTagHelper.GetOperationNameForHttpMethod(request.Method);

            this.activitySource.Start(activity, ActivityKind.Client);

            if (activity.IsAllDataRequested)
            {
                activity.SetCustomProperty(RequestCustomPropertyName, request);
                activity.SetTag(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method));
                activity.SetTag(SemanticConventions.AttributeHttpHost, HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                activity.SetTag(SemanticConventions.AttributeHttpUrl, request.RequestUri.OriginalString);

                if (this.options.SetHttpFlavor)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version));
                }
            }

            if (!(this.httpClientSupportsW3C && this.options.Propagator is TextMapPropagator))
            {
                this.options.Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), request, HttpRequestMessageHeaderValueSetter);
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                var requestTaskStatus = this.stopRequestStatusFetcher.Fetch(payload) as TaskStatus?;

                if (requestTaskStatus.HasValue)
                {
                    if (requestTaskStatus != TaskStatus.RanToCompletion)
                    {
                        if (requestTaskStatus == TaskStatus.Canceled)
                        {
                            activity.SetStatus(Status.Cancelled);
                        }
                        else if (requestTaskStatus != TaskStatus.Faulted)
                        {
                            // Faults are handled in OnException and should already have a span.Status of Unknown w/ Description.
                            activity.SetStatus(Status.Unknown);
                        }
                    }
                }

                if (this.stopResponseFetcher.Fetch(payload) is HttpResponseMessage response)
                {
                    activity.SetCustomProperty(ResponseCustomPropertyName, response);

                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode);

                    activity.SetStatus(
                        SpanHelper
                            .ResolveSpanStatusForHttpStatusCode((int)response.StatusCode)
                            .WithDescription(response.ReasonPhrase));
                }
            }

            this.activitySource.Stop(activity);
        }

        public override void OnException(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                if (!(this.stopExceptionFetcher.Fetch(payload) is Exception exc))
                {
                    HttpInstrumentationEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener), nameof(this.OnException));
                    return;
                }

                activity.SetCustomProperty(ExceptionCustomPropertyName, exc);

                if (exc is HttpRequestException)
                {
                    if (exc.InnerException is SocketException exception)
                    {
                        switch (exception.SocketErrorCode)
                        {
                            case SocketError.HostNotFound:
                                activity.SetStatus(Status.InvalidArgument.WithDescription(exc.Message));
                                return;
                        }
                    }

                    if (exc.InnerException != null)
                    {
                        activity.SetStatus(Status.Unknown.WithDescription(exc.Message));
                    }
                }
            }
        }
    }
}
