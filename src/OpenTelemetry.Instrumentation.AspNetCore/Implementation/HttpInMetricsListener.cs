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
using Microsoft.AspNetCore.Http;
#if NETCOREAPP
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal class HttpInMetricsListener : ListenerHandler
    {
        private readonly PropertyFetcher<HttpContext> stopContextFetcher = new("HttpContext");
        private readonly Meter meter;
        private readonly Histogram<double> httpServerDuration;

        public HttpInMetricsListener(string name, Meter meter)
            : base(name)
        {
            this.meter = meter;
            this.httpServerDuration = meter.CreateHistogram<double>("http.server.duration", "ms", "measures the duration of the inbound HTTP request");
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            HttpContext context = this.stopContextFetcher.Fetch(payload);
            if (context == null)
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), nameof(this.OnStopActivity));
                return;
            }

            // TODO: Prometheus pulls metrics by invoking the /metrics endpoint. Decide if it makes sense to suppress this.
            // Below is just a temporary way of achieving this suppression for metrics (we should consider suppressing traces too).
            // If we want to suppress activity from Prometheus then we should use SuppressInstrumentationScope.
            if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("metrics"))
            {
                return;
            }

            string host;

            if (context.Request.Host.Port is null or 80 or 443)
            {
                host = context.Request.Host.Host;
            }
            else
            {
                host = context.Request.Host.Host + ":" + context.Request.Host.Port;
            }

            TagList tags;

            // We need following directive as
            // RouteEndpoint is not available in netstandard2.0 and netstandard2.1
#if NETCOREAPP
            var target = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

            // TODO: This is just a minimal set of attributes. See the spec for additional attributes:
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#http-server
            if (!string.IsNullOrEmpty(target))
            {
                tags = new TagList
                {
                    { SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol) },
                    { SemanticConventions.AttributeHttpScheme, context.Request.Scheme },
                    { SemanticConventions.AttributeHttpMethod, context.Request.Method },
                    { SemanticConventions.AttributeHttpHost, host },
                    { SemanticConventions.AttributeHttpTarget, target },
                    { SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode.ToString() },
                };
            }
            else
            {
                tags = new TagList
                {
                    { SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocol(context.Request.Protocol) },
                    { SemanticConventions.AttributeHttpScheme, context.Request.Scheme },
                    { SemanticConventions.AttributeHttpMethod, context.Request.Method },
                    { SemanticConventions.AttributeHttpHost, host },
                    { SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode.ToString() },
                };
            }
#else
            tags = new TagList
            {
                { SemanticConventions.AttributeHttpFlavor, context.Request.Protocol },
                { SemanticConventions.AttributeHttpScheme, context.Request.Scheme },
                { SemanticConventions.AttributeHttpMethod, context.Request.Method },
                { SemanticConventions.AttributeHttpHost, host },
                { SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode.ToString() },
            };

#endif
            this.httpServerDuration.Record(activity.Duration.TotalMilliseconds, tags);
        }
    }
}
