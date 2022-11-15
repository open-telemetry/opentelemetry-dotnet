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
    internal sealed class HttpHandlerMetricsDiagnosticListener : ListenerHandler
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

                    TagList tags;
                    if (!request.RequestUri.IsDefaultPort)
                    {
                        tags = new TagList
                        {
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method)),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, request.RequestUri.Scheme),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version)),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port),
                        };
                    }
                    else
                    {
                        tags = new TagList
                        {
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method)),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, request.RequestUri.Scheme),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version)),
                            new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host),
                        };
                    }

                    this.httpClientDuration.Record(activity.Duration.TotalMilliseconds, tags);
                }
            }
        }
    }
}
