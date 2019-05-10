// <copyright file="HttpHandlerDiagnosticListener.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Collector.Dependencies.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using OpenTelemetry.Collector.Dependencies.Common;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Propagation;
    using OpenTelemetry.Trace.Sampler;

    internal class HttpHandlerDiagnosticListener : ListenerHandler
    {
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");

        private readonly IPropagationComponent propagationComponent;

        public HttpHandlerDiagnosticListener(ITracer tracer, Func<HttpRequestMessage, ISampler> samplerFactory, IPropagationComponent propagationComponent)
            : base("HttpHandlerDiagnosticListener", tracer, samplerFactory)
        {
            this.propagationComponent = propagationComponent;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                // Debug.WriteLine("request is null");
                return;
            }

            this.Tracer.SpanBuilder(request.RequestUri.AbsolutePath, SpanKind.Client).SetSampler(this.SamplerFactory(request)).StartScopedSpan(out ISpan span);
            span.PutHttpMethodAttribute(request.Method.ToString());
            span.PutHttpHostAttribute(request.RequestUri.Host, request.RequestUri.Port);
            span.PutHttpPathAttribute(request.RequestUri.AbsolutePath);
            request.Headers.TryGetValues("User-Agent", out IEnumerable<string> userAgents);
            span.PutHttpUserAgentAttribute(userAgents?.FirstOrDefault());
            span.PutHttpRawUrlAttribute(request.RequestUri.OriginalString);

            this.propagationComponent.TextFormat.Inject<HttpRequestMessage>(span.Context, request, (r, k, v) => r.Headers.Add(k, v));
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            var span = this.Tracer.CurrentSpan;

            if (span == null)
            {
                DependenciesCollectorEventSource.Log.NullContext();
                return;
            }

            var requestTaskStatus = this.stopRequestStatusFetcher.Fetch(payload) as TaskStatus?;

            if (requestTaskStatus.HasValue)
            {
                if (requestTaskStatus != TaskStatus.RanToCompletion)
                {
                    span.Status = Status.Unknown;

                    if (requestTaskStatus == TaskStatus.Canceled)
                    {
                        span.Status = Status.Cancelled;
                    }
                }
            }

            if (!(this.stopResponseFetcher.Fetch(payload) is HttpResponseMessage response))
            {
                // response could be null for DNS issues, timeouts, etc...
                // TODO: how do we make sure we will not close a scope that wasn't opened?

                span.End();
                return;
            }

            span.PutHttpStatusCode((int)response.StatusCode, response.ReasonPhrase);

            span.End();
        }

        public override void OnException(Activity activity, object payload)
        {
            if (!(this.stopExceptionFetcher.Fetch(payload) is Exception exc))
            {
                // Debug.WriteLine("response is null");
                return;
            }

            var span = this.Tracer.CurrentSpan;

            if (span == null)
            {
                // TODO: Notify that span got lost
                return;
            }

            if (exc is HttpRequestException)
            {
                // TODO: on netstandard this will be System.Net.Http.WinHttpException: The server name or address could not be resolved
                if (exc.InnerException is WebException &&
                    ((WebException)exc.InnerException).Status == WebExceptionStatus.NameResolutionFailure)
                {
                    span.Status = Status.InvalidArgument;
                }
                else if (exc.InnerException != null)
                {
                    span.Status = Status.Unknown.WithDescription(exc.Message);
                }
            }
        }
    }
}