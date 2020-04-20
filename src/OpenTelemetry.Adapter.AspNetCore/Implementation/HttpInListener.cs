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
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Adapter.AspNetCore.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        private static readonly string UnknownHostName = "UNKNOWN-HOST";
        private readonly PropertyFetcher startContextFetcher = new PropertyFetcher("HttpContext");
        private readonly PropertyFetcher stopContextFetcher = new PropertyFetcher("HttpContext");
        private readonly PropertyFetcher beforeActionActionDescriptorFetcher = new PropertyFetcher("actionDescriptor");
        private readonly PropertyFetcher beforeActionAttributeRouteInfoFetcher = new PropertyFetcher("AttributeRouteInfo");
        private readonly PropertyFetcher beforeActionTemplateFetcher = new PropertyFetcher("Template");
        private readonly bool hostingSupportsW3C = false;
        private readonly AspNetCoreAdapterOptions options;

        public HttpInListener(string name, Tracer tracer, AspNetCoreAdapterOptions options)
            : base(name, tracer)
        {
            this.hostingSupportsW3C = typeof(HttpRequest).Assembly.GetName().Version.Major >= 3;
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";
            var context = this.startContextFetcher.Fetch(payload) as HttpContext;

            if (context == null)
            {
                AdapterEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (this.options.RequestFilter != null && !this.options.RequestFilter(context))
            {
                AdapterEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                return;
            }

            var request = context.Request;

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md
            var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";

            TelemetrySpan span;
            if (this.hostingSupportsW3C && this.options.TextFormat is TraceContextFormat)
            {
                this.Tracer.StartActiveSpanFromActivity(path, Activity.Current, SpanKind.Server, out span);
            }
            else
            {
                var ctx = this.options.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => r.Headers[name]);

                this.Tracer.StartActiveSpan(path, ctx, SpanKind.Server, out span);
            }

            if (span.IsRecording)
            {
                // Note, route is missing at this stage. It will be available later
                span.PutHttpHostAttribute(request.Host.Host, request.Host.Port ?? 80);
                span.PutHttpMethodAttribute(request.Method);
                span.PutHttpPathAttribute(path);

                var userAgent = request.Headers["User-Agent"].FirstOrDefault();
                span.PutHttpUserAgentAttribute(userAgent);
                span.PutHttpRawUrlAttribute(GetUri(request));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                AdapterEventSource.Log.NullOrBlankSpan(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (span.IsRecording)
            {
                if (!(this.stopContextFetcher.Fetch(payload) is HttpContext context))
                {
                    AdapterEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                    return;
                }

                var response = context.Response;

                span.PutHttpStatusCode(response.StatusCode, response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase);
            }

            span.End();
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            if (name == "Microsoft.AspNetCore.Mvc.BeforeAction")
            {
                var span = this.Tracer.CurrentSpan;

                if (span == null)
                {
                    AdapterEventSource.Log.NullOrBlankSpan(name);
                    return;
                }

                if (span.IsRecording)
                {
                    // See https://github.com/aspnet/Mvc/blob/2414db256f32a047770326d14d8b0e2afd49ba49/src/Microsoft.AspNetCore.Mvc.Core/MvcCoreDiagnosticSourceExtensions.cs#L36-L44
                    // Reflection accessing: ActionDescriptor.AttributeRouteInfo.Template
                    // The reason to use reflection is to avoid a reference on MVC package.
                    // This package can be used with non-MVC apps and this logic simply wouldn't run.
                    // Taking reference on MVC will increase size of deployment for non-MVC apps.
                    var actionDescriptor = this.beforeActionActionDescriptorFetcher.Fetch(payload);
                    var attributeRouteInfo = this.beforeActionAttributeRouteInfoFetcher.Fetch(actionDescriptor);
                    var template = this.beforeActionTemplateFetcher.Fetch(attributeRouteInfo) as string;

                    if (!string.IsNullOrEmpty(template))
                    {
                        // override the span name that was previously set to the path part of URL.
                        span.UpdateName(template);

                        span.PutHttpRouteAttribute(template);
                    }

                    // TODO: Should we get values from RouteData?
                    // private readonly PropertyFetcher beforActionRouteDataFetcher = new PropertyFetcher("routeData");
                    // var routeData = this.beforActionRouteDataFetcher.Fetch(payload) as RouteData;
                }
            }
        }

        private static string GetUri(HttpRequest request)
        {
            var builder = new StringBuilder();

            builder.Append(request.Scheme).Append("://");

            if (request.Host.HasValue)
            {
                builder.Append(request.Host.Value);
            }
            else
            {
                // HTTP 1.0 request with NO host header would result in empty Host.
                // Use placeholder to avoid incorrect URL like "http:///"
                builder.Append(UnknownHostName);
            }

            if (request.PathBase.HasValue)
            {
                builder.Append(request.PathBase.Value);
            }

            if (request.Path.HasValue)
            {
                builder.Append(request.Path.Value);
            }

            if (request.QueryString.HasValue)
            {
                builder.Append(request.QueryString);
            }

            return builder.ToString();
        }
    }
}
