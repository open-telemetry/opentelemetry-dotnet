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

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal sealed class HttpInMetricsListener : ListenerHandler
    {
        private const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
        private const string OnStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";

        private readonly Meter meter;
        private readonly Histogram<double> httpServerDuration;
        private readonly UpDownCounter<long> httpServerActiveRequests;

        public HttpInMetricsListener(string name, Meter meter)
            : base(name)
        {
            this.meter = meter;
            this.httpServerDuration = meter.CreateHistogram<double>("http.server.duration", "ms", "measures the duration of the inbound HTTP request");
            this.httpServerActiveRequests = meter.CreateUpDownCounter<long>("http.server.active_requests", "measures the number of concurrent HTTP requests that are currently in-flight");
        }

        public override void OnEventWritten(string name, object payload)
        {
            if (name == OnStartEvent)
            {
                var context = payload as HttpContext;
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), nameof(this.OnEventWritten));
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

                if (context.Request.Host.HasValue)
                {
                    tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, context.Request.Host.Host));
                }

                // Add request to the currently active request count.
                this.httpServerActiveRequests.Add(1, tags);
            }
            else if (name == OnStopEvent)
            {
                var context = payload as HttpContext;
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), nameof(this.OnEventWritten));
                    return;
                }

                // TODO: Prometheus pulls metrics by invoking the /metrics endpoint. Decide if it makes sense to suppress this.
                // Below is just a temporary way of achieving this suppression for metrics (we should consider suppressing traces too).
                // If we want to suppress activity from Prometheus then we should use SuppressInstrumentationScope.
                if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("metrics"))
                {
                    return;
                }

                TagList httpServerDurationTags = default;

                httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol)));
                httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, context.Request.Scheme));
                httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, context.Request.Method));
                httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode));

                if (context.Request.Host.HasValue)
                {
                    httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, context.Request.Host.Host));

                    if (context.Request.Host.Port is not null && context.Request.Host.Port != 80 && context.Request.Host.Port != 443)
                    {
                        httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostPort, context.Request.Host.Port));
                    }
                }
#if NET6_0_OR_GREATER
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                if (!string.IsNullOrEmpty(route))
                {
                    httpServerDurationTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, route));
                }
#endif

                TagList httpServerActiveRequestsTags = default;

                httpServerActiveRequestsTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol)));
                httpServerActiveRequestsTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, context.Request.Scheme));
                httpServerActiveRequestsTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, context.Request.Method));

                if (context.Request.Host.HasValue)
                {
                    httpServerActiveRequestsTags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, context.Request.Host.Host));
                }

                this.httpServerDuration.Record(Activity.Current.Duration.TotalMilliseconds, httpServerDurationTags);

                // Remove the request from currently active requests count
                this.httpServerActiveRequests.Add(-1, httpServerActiveRequestsTags);
            }
        }
    }
}
