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

namespace OpenTelemetry.Collector.AspNetCore.Implementation
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using OpenTelemetry.Collector.AspNetCore.Common;
    using OpenTelemetry.Trace;

    internal class HttpInListener : ListenerHandler
    {
        private static readonly string UnknownHostName = "UNKNOWN-HOST";
        private readonly PropertyFetcher startContextFetcher = new PropertyFetcher("HttpContext");
        private readonly PropertyFetcher stopContextFetcher = new PropertyFetcher("HttpContext");
        private readonly PropertyFetcher beforeActionActionDescriptorFetcher = new PropertyFetcher("actionDescriptor");
        private readonly PropertyFetcher beforeActionAttributeRouteInfoFetcher = new PropertyFetcher("AttributeRouteInfo");
        private readonly PropertyFetcher beforeActionTemplateFetcher = new PropertyFetcher("Template");
        private readonly bool hostingSupportsW3C = false;

        public HttpInListener(ITracer tracer, Func<HttpRequest, ISampler> samplerFactory)
            : base("Microsoft.AspNetCore", tracer, samplerFactory)
        {
            this.hostingSupportsW3C = typeof(HttpRequest).Assembly.GetName().Version.Major >= 3;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            var context = this.startContextFetcher.Fetch(payload) as HttpContext;

            if (context == null)
            {
                // Debug.WriteLine("context is null");
                return;
            }

            var request = context.Request;

            SpanContext ctx = null;
            if (!this.hostingSupportsW3C)
            {
                ctx = this.Tracer.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => r.Headers[name]);
            }

            // see the spec https://github.com/open-telemetry/OpenTelemetry-specs/blob/master/trace/HTTP.md
            var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";

            var spanBuilder = this.Tracer.SpanBuilder(path)
                .SetSpanKind(SpanKind.Server)
                .SetSampler(this.SamplerFactory(request));

            if (this.hostingSupportsW3C)
            {
                spanBuilder.SetCreateChild(false);
            }
            else
            {
                spanBuilder.SetParent(ctx);
            }

            var span = spanBuilder.StartSpan();
            this.Tracer.WithSpan(span);

            if (span.IsRecordingEvents)
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
            var span = this.Tracer.CurrentSpan;

            if (span == null || span == BlankSpan.Instance)
            {
                AspNetCoreCollectorEventSource.Log.NullOrBlankSpan("HttpInListener.OnStopActivity");
                return;
            }

            if (!span.IsRecordingEvents)
            {
                span.End();
                return;
            }

            if (!(this.stopContextFetcher.Fetch(payload) is HttpContext context))
            {
                AspNetCoreCollectorEventSource.Log.NullHttpContext("HttpInListener.OnStopActivity");
                return;
            }

            var response = context.Response;

            span.PutHttpStatusCode(response.StatusCode, response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase);
            span.End();
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            if (name == "Microsoft.AspNetCore.Mvc.BeforeAction")
            {
                var span = this.Tracer.CurrentSpan;

                if (span == null)
                {
                    AspNetCoreCollectorEventSource.Log.NullOrBlankSpan(name);
                    return;
                }

                if (span.IsRecordingEvents)
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
