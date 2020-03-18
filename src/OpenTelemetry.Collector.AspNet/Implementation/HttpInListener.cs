// <copyright file="HttpInListener.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Web;
using System.Web.Routing;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.AspNet.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        private readonly PropertyFetcher routeFetcher = new PropertyFetcher("Route");
        private readonly PropertyFetcher routeTemplateFetcher = new PropertyFetcher("RouteTemplate");
        private readonly AspNetCollectorOptions options;

        public HttpInListener(string name, Tracer tracer, AspNetCollectorOptions options)
            : base(name, tracer)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";

            var context = HttpContext.Current;
            if (context == null)
            {
                CollectorEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (this.options.RequestFilter != null && !this.options.RequestFilter(context))
            {
                CollectorEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                return;
            }

            var request = context.Request;
            var requestValues = request.Unvalidated;

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md
            var path = requestValues.Path;

            TelemetrySpan span;
            if (this.options.TextFormat is TraceContextFormat)
            {
                this.Tracer.StartActiveSpanFromActivity(path, Activity.Current, SpanKind.Server, out span);
            }
            else
            {
                var ctx = this.options.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => requestValues.Headers.GetValues(name));

                this.Tracer.StartActiveSpan(path, ctx, SpanKind.Server, out span);
            }

            if (span.IsRecording)
            {
                span.PutHttpHostAttribute(request.Url.Host, request.Url.Port);
                span.PutHttpMethodAttribute(request.HttpMethod);
                span.PutHttpPathAttribute(path);

                span.PutHttpUserAgentAttribute(request.UserAgent);
                span.PutHttpRawUrlAttribute(request.Url.ToString());
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                CollectorEventSource.Log.NullOrBlankSpan(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (span.IsRecording)
            {
                var context = HttpContext.Current;
                if (context == null)
                {
                    CollectorEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                    return;
                }

                var response = context.Response;

                span.PutHttpStatusCode(response.StatusCode, response.StatusDescription);

                var routeData = context.Request.RequestContext.RouteData;

                string template = null;
                if (routeData.Values.TryGetValue("MS_SubRoutes", out object msSubRoutes))
                {
                    // WebAPI attribute routing flows here. Use reflection to not take a dependency on microsoft.aspnet.webapi.core\[version]\lib\[framework]\System.Web.Http.

                    if (msSubRoutes is Array attributeRouting && attributeRouting.Length == 1)
                    {
                        var subRouteData = attributeRouting.GetValue(0);

                        var route = this.routeFetcher.Fetch(subRouteData);
                        template = this.routeTemplateFetcher.Fetch(route) as string;
                    }
                }
                else if (routeData.Route is Route route)
                {
                    // MVC + WebAPI traditional routing & MVC attribute routing flow here.

                    template = route.Url;
                }

                if (!string.IsNullOrEmpty(template))
                {
                    // Override the span name that was previously set to the path part of URL.
                    span.UpdateName(template);

                    span.PutHttpRouteAttribute(template);
                }
            }

            span.End();
        }
    }
}
