﻿// <copyright file="HttpInListener.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
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
        private readonly AspNetCoreInstrumentationOptions options;

        public HttpInListener(string name, AspNetCoreInstrumentationOptions options)
            : base(name, null)
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
                InstrumentationEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (this.options.RequestFilter != null && !this.options.RequestFilter(context))
            {
                InstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                return;
            }

            // TODO: the line below once .NET ships new Activity
            // Or do reflection now.
            // activity.ActivityKind = ActivityKind.Server

            var request = context.Request;

            if (!this.hostingSupportsW3C || !(this.options.TextFormat is TraceContextFormat))
            {
                // This requires to ignore the current activity and create a new one
                // using the context extracted from w3ctraceprent header or
                // using the format TextFormat supports.
                // TODO: actually implement code doing the above.

                /*
                var ctx = this.options.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => r.Headers[name]);

                this.Tracer.StartActiveSpan(path, ctx, SpanKind.Server, out span);
                */
            }

            if (activity.IsAllDataRequested)
            {
                // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md
                var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
                activity.DisplayName = path;

                if (request.Host.Port == 80 || request.Host.Port == 443)
                {
                    activity.AddTag("http.host", request.Host.Host);
                }
                else
                {
                    activity.AddTag("http.host", request.Host.Host + ":" + request.Host.Port);
                }

                activity.AddTag("http.method", request.Method);
                activity.AddTag("http.path", path);

                var userAgent = request.Headers["User-Agent"].FirstOrDefault();
                activity.AddTag("http.user_agent", userAgent);
                activity.AddTag("http.url", GetUri(request));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";

            if (activity.IsAllDataRequested)
            {
                if (!(this.stopContextFetcher.Fetch(payload) is HttpContext context))
                {
                    InstrumentationEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                    return;
                }

                var response = context.Response;
                activity.AddTag(SpanAttributeConstants.HttpStatusCodeKey, response.StatusCode.ToString());

                Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode);
                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase);
            }
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            if (name == "Microsoft.AspNetCore.Mvc.BeforeAction")
            {
                if (activity.IsAllDataRequested)
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
                        activity.DisplayName = template;
                        activity.AddTag(SpanAttributeConstants.HttpRouteKey, template);
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
