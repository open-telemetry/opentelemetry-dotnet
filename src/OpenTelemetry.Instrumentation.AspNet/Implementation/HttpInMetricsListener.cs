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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Web;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation
{
    internal sealed class HttpInMetricsListener : IDisposable
    {
        private readonly Histogram<double> httpServerDuration;

        public HttpInMetricsListener(Meter meter)
        {
            this.httpServerDuration = meter.CreateHistogram<double>("http.server.duration", "ms", "measures the duration of the inbound HTTP request");
            TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
        }

        public void Dispose()
        {
            TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
        }

        private void OnStopActivity(Activity activity, HttpContext context)
        {
            // TODO: This is just a minimal set of attributes. See the spec for additional attributes:
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#http-server
            var tags = new TagList
            {
                { SemanticConventions.AttributeHttpMethod, context.Request.HttpMethod },
                { SemanticConventions.AttributeHttpScheme, context.Request.Url.Scheme },
                { SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode },
            };

            this.httpServerDuration.Record(activity.Duration.TotalMilliseconds, tags);
        }
    }
}
