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

using System.Diagnostics;
using System.Diagnostics.Metrics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Implementation
{
    internal sealed class HttpHandlerMetricsDiagnosticListener : ListenerHandler
    {
        internal const string OnStopEvent = "System.Net.Http.HttpRequestOut.Stop";

        private readonly PropertyFetcher<HttpResponseMessage> stopResponseFetcher = new("Response");
        private readonly Histogram<double> httpClientDuration;
        private readonly HttpClientInstrumentationMeterOptions options;

        public HttpHandlerMetricsDiagnosticListener(string name, Meter meter, HttpClientInstrumentationMeterOptions options)
            : base(name)
        {
            this.options = options;
            this.httpClientDuration = meter.CreateHistogram<double>("http.client.duration", "ms", "measures the duration of the outbound HTTP request");
        }

        public override void OnEventWritten(string name, object payload)
        {
            if (name == OnStopEvent)
            {
                this.OnStopActivity(Activity.Current, payload);
            }
        }

        private void OnStopActivity(Activity activity, object payload)
        {
            if (Sdk.SuppressInstrumentation)
            {
                return;
            }

            if (!this.stopResponseFetcher.TryFetch(payload, out HttpResponseMessage response) || response == null)
            {
                // TODO: logging?
                return;
            }

            var request = response.RequestMessage;
            var tags = new List<KeyValuePair<string, object>>
            {
                new(SemanticConventions.AttributeHttpMethod, HttpTagHelper.GetNameForHttpMethod(request.Method)),
                new(SemanticConventions.AttributeHttpScheme, request.RequestUri.Scheme),
                new(SemanticConventions.AttributeHttpStatusCode, (int)response.StatusCode),
                new(SemanticConventions.AttributeHttpFlavor, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version)),
                new(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host),
            };

            if (!request.RequestUri.IsDefaultPort)
            {
                tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port));
            }

            if (!this.TryFilterHttpRequestMessage(activity.OperationName, request))
            {
                return;
            }

            this.EnrichWithHttpRequestMessage(tags, request);

            this.httpClientDuration.Record(activity.Duration.TotalMilliseconds, tags.ToArray());
        }

        /// <summary>
        /// Gets or sets a filter function that determines whether or not to
        /// collect telemetry on a per request basis.
        /// </summary>
        /// <param name="operationName">The name of operation.</param>
        /// <param name="request">Proceed <see cref="HttpRequestMessage"/>.</param>
        /// <returns>
        /// <list type="bullet">
        /// <item>The return value for the filter function is interpreted as:
        /// <list type="bullet">
        /// <item>If filter returns <see langword="true" />, the request is
        /// collected.</item>
        /// <item>If filter returns <see langword="false" /> or throws an
        /// exception the request is NOT collected.</item>
        /// </list></item>
        /// </list>
        /// </returns>
        private bool TryFilterHttpRequestMessage(string operationName, HttpRequestMessage request)
        {
            try
            {
                var shouldCollect = this.options.FilterHttpRequestMessage?.Invoke(request) ?? true;

                if (shouldCollect)
                {
                    return true;
                }

                HttpInstrumentationEventSource.Log.RequestIsFilteredOut(operationName);
                return false;
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                return false;
            }
        }

        private void EnrichWithHttpRequestMessage(List<KeyValuePair<string, object>> tags, HttpRequestMessage request)
        {
            try
            {
                this.options.EnrichWithHttpRequestMessage?.Invoke(tags, request);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }
}
