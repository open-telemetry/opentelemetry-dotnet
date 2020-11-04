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
        private readonly PropertyFetcher<HttpContext> stopContextFetcher = new PropertyFetcher<HttpContext>("HttpContext");
        private readonly AspNetCoreInstrumentationOptions options;
        private readonly Meter meter;

        private MeasureMetric<double> httpServerDuration;

        public HttpInMetricsListener(string name, AspNetCoreInstrumentationOptions options, Meter meter)
            : base(name)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.meter = meter;
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

            var labels = new Dictionary<string, string>(StringComparer.Ordinal);

            labels[SemanticConventions.AttributeHttpMethod] = context.Request.Method;

            labels[SemanticConventions.AttributeHttpScheme] = context.Request.Scheme;

            if (context.Request.Host.HasValue)
            {
                labels[SemanticConventions.AttributeHttpHost] = context.Request.Host.Value;
                labels[SemanticConventions.AttributeNetHostName] = context.Request.Host.Host;

                if (context.Request.Host.Port.HasValue)
                {
                    labels[SemanticConventions.AttributeNetHostPort] = context.Request.Host.Port.ToString();
                }
            }

            labels[SemanticConventions.AttributeHttpStatusCode] = context.Response.StatusCode.ToString();
            labels[SemanticConventions.AttributeHttpFlavor] = context.Request.Protocol;

            // TODO: Decide if/how to handle http.server_name. Spec seems to suggest
            // preference for net.host.name.
            // labels[SemanticConventions.AttributeHttpServerName] = string.Empty;

            // TODO: Decide if we want http.url. It seems awful from a cardinality perspective.
            // Retrieving the route data and setting http.target probably makes more sense.
            // labels[SemanticConventions.AttributeHttpUrl] = string.Empty;

            // TODO: Retrieve the route.
            // labels[SemanticConventions.AttributeHttpTarget] = string.Empty;

            // TODO: Ideally we could do this in the constructor. However, the instrumentation is usually instantiated
            // prior to invoking MeterProvider.SetDefault. Setting the default meter provider is required before metrics
            // can be created.
            // This should be safe in the meantime since CreateDoubleMeasure uses a ConcurrentDictionary behind the scenes.
            if (this.httpServerDuration == null)
            {
                this.httpServerDuration = this.meter.CreateDoubleMeasure(SemanticConventions.MetricHttpServerDuration);
            }

            this.httpServerDuration.Record(new SpanContext(activity.Context), activity.Duration.TotalMilliseconds, labels);
        }
    }
}
