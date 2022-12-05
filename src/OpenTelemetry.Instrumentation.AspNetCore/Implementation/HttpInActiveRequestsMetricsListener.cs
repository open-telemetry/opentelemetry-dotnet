// <copyright file="HttpInActiveRequestsMetricsListener.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal sealed class HttpInActiveRequestsMetricsListener : ListenerHandler
    {
        private const string OnStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
        private const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";

        private readonly Meter meter;
        private readonly UpDownCounter<long> httpServerActiveRequests;

        public HttpInActiveRequestsMetricsListener(string name, Meter meter)
            : base(name)
        {
            this.meter = meter;
            this.httpServerActiveRequests = meter.CreateUpDownCounter<long>("http.server.active_requests", "measures the number of concurrent HTTP requests that are currently in-flight");
        }

        public override void OnEventWritten(string name, object payload)
        {
            if (name == OnStartEvent)
            {
                var context = payload as HttpContext;
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInActiveRequestsMetricsListener), nameof(this.OnEventWritten));
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

                this.httpServerActiveRequests.Add(1, tags);
            }
            else if (name == OnStopEvent)
            {
                var context = payload as HttpContext;
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInDurationMetricsListener), nameof(this.OnEventWritten));
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

                this.httpServerActiveRequests.Add(-1, tags);
            }
        }
    }
}
