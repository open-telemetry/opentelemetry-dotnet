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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal class HttpInMetricsListener : ListenerHandler
    {
        private static readonly Meter Meter = MeterProvider.Default.GetMeter("AspNetCore");

        // TODO: The spec says this metric should be http.server.duration. Though, Promethus does not support dots
        // in the metric name. For now, using a name that make testing with Promethus easy. Prometheus exporter should
        // deal with normalizing the names for its purpose.
        private static readonly MeasureMetric<double> Measure = Meter.CreateDoubleMeasure("http_server_duration");

        private readonly PropertyFetcher<HttpContext> stopContextFetcher = new PropertyFetcher<HttpContext>("HttpContext");
        private readonly AspNetCoreInstrumentationOptions options;

        public HttpInMetricsListener(string name, AspNetCoreInstrumentationOptions options)
            : base(name)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
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
            // Below is just a temporary way of achieving this supression for metrics (we should consider suppressing traces too).
            // If we want to suppress activity from Prometheus then we should use SuppressInstrumentationScope.
            if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("metrics"))
            {
                return;
            }

            var labels = new Dictionary<string, string>();

            labels["http.method"] = context.Request.Method;

            labels["http.scheme"] = context.Request.Scheme;

            if (context.Request.Host.HasValue)
            {
                labels["http.host"] = context.Request.Host.Value;
                labels["net.host.name"] = context.Request.Host.Host;

                if (context.Request.Host.Port.HasValue)
                {
                    labels["net.host.port"] = context.Request.Host.Port.ToString();
                }
            }

            labels["http.status_code"] = context.Response.StatusCode.ToString();
            labels["http.flavor"] = context.Request.Protocol;

            // TODO: Decide if/how to handle http.server_name. Spec seems to suggest
            // preference for net.host.name.
            labels["http.server_name"] = string.Empty;

            // TODO: Decide if we want http.url. It seems awful from a cardinality perspective.
            // Retrieving the route data and setting http.target probably makes more sense.
            labels["http.url"] = string.Empty;

            // TODO: Retrieve the route.
            labels["http.target"] = string.Empty;

            Measure.Record(new SpanContext(activity.Context), activity.Duration.TotalMilliseconds, labels);
        }
    }
}
