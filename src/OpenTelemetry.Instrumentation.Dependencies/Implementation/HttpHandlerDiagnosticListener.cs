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
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class HttpHandlerDiagnosticListener : ListenerHandler
    {
        private static readonly Regex CoreAppMajorVersionCheckRegex = new Regex("^\\.NETCoreApp,Version=v(\\d+)\\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // hard-coded Sampler here, just to prototype.
        // Either .NET will provide an new API to avoid Instrumentation being aware of sampling.
        // or we'll move the Sampler to come from OpenTelemetryBuilder, and not hardcoded.
        private readonly ActivitySampler sampler = new AlwaysOnActivitySampler();

        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");
        private readonly bool httpClientSupportsW3C = false;
        private readonly HttpClientInstrumentationOptions options;

        public HttpHandlerDiagnosticListener(HttpClientInstrumentationOptions options)
            : base("HttpHandlerDiagnosticListener", null)
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

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            activity.GetType().GetProperty("Kind").SetValue(activity, ActivityKind.Client);
            activity.DisplayName = HttpTagHelper.GetOperationNameForHttpMethod(request.Method);

            var samplingParameters = new ActivitySamplingParameters(
                activity.Context,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.Tags,
                activity.Links);

            // TODO: Find a way to avoid Instrumentation being tied to Sampler
            var samplingDecision = this.sampler.ShouldSample(samplingParameters);
            activity.IsAllDataRequested = samplingDecision.IsSampled;
            if (samplingDecision.IsSampled)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SpanAttributeConstants.ComponentKey, "http");
                activity.AddTag(SpanAttributeConstants.HttpMethodKey, HttpTagHelper.GetNameForHttpMethod(request.Method));
                activity.AddTag(SpanAttributeConstants.HttpHostKey, HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                activity.AddTag(SpanAttributeConstants.HttpUrlKey, request.RequestUri.OriginalString);

                if (this.options.SetHttpFlavor)
                {
                    activity.AddTag(SpanAttributeConstants.HttpFlavorKey, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version));
                }
            }

            if (!(this.httpClientSupportsW3C && this.options.TextFormat is TraceContextFormatActivity))
            {
                this.options.TextFormat.Inject(activity.Context, request, (r, k, v) => r.Headers.Add(k, v));
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
                            Status status = Status.Cancelled;
                            activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                        }
                        else if (requestTaskStatus != TaskStatus.Faulted)
                        {
                            // Faults are handled in OnException and should already have a span.Status of Unknown w/ Description.
                            Status status = Status.Unknown;
                            activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                        }
                    }
                }

                if (this.stopResponseFetcher.Fetch(payload) is HttpResponseMessage response)
                {
                    // response could be null for DNS issues, timeouts, etc...
                    activity.AddTag(SpanAttributeConstants.HttpStatusCodeKey, response.StatusCode.ToString());

                    Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode);
                    activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                    activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, response.ReasonPhrase);
                }
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnException";

            if (activity.IsAllDataRequested)
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
                                Status status = Status.InvalidArgument;
                                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                                activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, exc.Message);
                                return;
                        }
                    }

                    if (exc.InnerException != null)
                    {
                        Status status = Status.Unknown;
                        activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                        activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, exc.Message);
                    }
                }
            }
        }
    }
}
