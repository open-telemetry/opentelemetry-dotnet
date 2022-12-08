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
#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal sealed class HttpInMetricsListener : ListenerHandler
    {
        private const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";

        private readonly Meter meter;
        private readonly AspNetCoreMetricsInstrumentationOptions options;
        private readonly Histogram<double> httpServerDuration;

        internal HttpInMetricsListener(string name, Meter meter, AspNetCoreMetricsInstrumentationOptions options)
            : base(name)
        {
            this.meter = meter;
            this.options = options;
            this.httpServerDuration = meter.CreateHistogram<double>("http.server.duration", "ms", "measures the duration of the inbound HTTP request");
        }

        public override void OnEventWritten(string name, object payload)
        {
            if (name == OnStopEvent)
            {
                var context = payload as HttpContext;
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInMetricsListener), nameof(this.OnEventWritten));
                    return;
                }

                try
                {
                    if (this.options.Filter?.Invoke(context) == false)
                    {
                        AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(Activity.Current.OperationName);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.RequestFilterException(ex);
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
                tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode));

                if (context.Request.Host.HasValue)
                {
                    tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, context.Request.Host.Host));

                    if (context.Request.Host.Port is not null && context.Request.Host.Port != 80 && context.Request.Host.Port != 443)
                    {
                        tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostPort, context.Request.Host.Port));
                    }
                }
#if NET6_0_OR_GREATER
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                if (!string.IsNullOrEmpty(route))
                {
                    tags.Add(new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, route));
                }
#endif
                if (this.options.Enrich != null)
                {
                    try
                    {
                        this.options.Enrich(context, ref tags);
                    }
                    catch (Exception ex)
                    {
                        AspNetCoreInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }

                this.httpServerDuration.Record(Activity.Current.Duration.TotalMilliseconds, tags);
            }
        }
    }
}
