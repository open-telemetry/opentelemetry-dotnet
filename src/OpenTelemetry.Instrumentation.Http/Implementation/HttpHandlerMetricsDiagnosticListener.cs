// <copyright file="HttpHandlerMetricsDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Implementation
{
    internal class HttpHandlerMetricsDiagnosticListener : ListenerHandler
    {
        internal const string OnStopEvent = "System.Net.Http.HttpRequestOut.Stop";

        private readonly PropertyFetcher<HttpResponseMessage> stopResponseFetcher = new("Response");
        private readonly Histogram<double> httpClientDuration;

        public HttpHandlerMetricsDiagnosticListener(string name, Meter meter)
            : base(name)
        {
            this.httpClientDuration = meter.CreateHistogram<double>("http.client.duration", "ms", "measures the duration of the outbound HTTP request");
        }

        public override void OnEventWritten(string name, object payload)
        {
            if (name == OnStopEvent)
            {
                if (Sdk.SuppressInstrumentation)
                {
                    return;
                }

                var activity = Activity.Current;
                if (this.stopResponseFetcher.TryFetch(payload, out HttpResponseMessage response) && response != null)
                {
                    var request = response.RequestMessage;

                    // TODO: This is just a minimal set of attributes. See the spec for additional attributes:
                    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#http-client
                    var tags = new KeyValuePair<string, object>[]
                    {
                        new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method)),
                        new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, request.RequestUri.Scheme),
                        new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode),
                        new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version)),
                    };

                    this.httpClientDuration.Record(activity.Duration.TotalMilliseconds, tags);
                }
            }
        }
    }
}
