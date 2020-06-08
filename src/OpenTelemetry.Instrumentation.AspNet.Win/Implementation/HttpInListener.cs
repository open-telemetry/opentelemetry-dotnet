// <copyright file="HttpInListener.cs" company="OpenTelemetry Authors">
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
using System.Web;
using System.Web.Routing;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        // hard-coded Sampler here, just to prototype.
        // Either .NET will provide an new API to avoid Instrumentation being aware of sampling.
        // or we'll move the Sampler to come from OpenTelemetryBuilder, and not hardcoded.
        private readonly ActivitySampler sampler = new AlwaysOnActivitySampler();
        private readonly PropertyFetcher routeFetcher = new PropertyFetcher("Route");
        private readonly PropertyFetcher routeTemplateFetcher = new PropertyFetcher("RouteTemplate");
        private readonly AspNetInstrumentationOptions options;

        public HttpInListener(string name, AspNetInstrumentationOptions options)
            : base(name, null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";

            var context = HttpContext.Current;
            if (context == null)
            {
                InstrumentationEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                return;
            }

            if (this.options.RequestFilter != null && !this.options.RequestFilter(context))
            {
                // TODO: These filters won't prevent the activity from being tracked
                // as they are fired anyway.
                InstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                return;
            }

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            activity.GetType().GetProperty("Kind").SetValue(activity, ActivityKind.Server);

            var request = context.Request;
            var requestValues = request.Unvalidated;

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md
            var path = requestValues.Path;
            activity.DisplayName = path;

            var samplingParameters = new ActivitySamplingParameters(
                activity.Context,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.Tags,
                activity.Links);

            // TODO: Find a way to avoid Instrumentation being tied to Sampler
            var samplingDecision = this.sampler.ShouldSample(samplingParameters);
            activity.IsAllDataRequested = samplingDecision.IsSampled;
            if (samplingDecision.IsSampled)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            if (!(this.options.TextFormat is TraceContextFormat))
            {
                // This requires to ignore the current activity and create a new one
                // using the context extracted using the format TextFormat supports.
                // TODO: actually implement code doing the above.
                /*
                var ctx = this.options.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => requestValues.Headers.GetValues(name));

                this.Tracer.StartActiveSpan(path, ctx, SpanKind.Server, out span);
                */
            }

            if (activity.IsAllDataRequested)
            {
                if (request.Url.Port == 80 || request.Url.Port == 443)
                {
                    activity.AddTag(SpanAttributeConstants.HttpHostKey, request.Url.Host);
                }
                else
                {
                    activity.AddTag(SpanAttributeConstants.HttpHostKey, request.Url.Host + ":" + request.Url.Port);
                }

                activity.AddTag(SpanAttributeConstants.HttpMethodKey, request.HttpMethod);
                activity.AddTag(SpanAttributeConstants.HttpPathKey, path);
                activity.AddTag(SpanAttributeConstants.HttpUserAgentKey, request.UserAgent);
                activity.AddTag(SpanAttributeConstants.HttpUrlKey, request.Url.ToString());
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";

            if (activity.IsAllDataRequested)
            {
                var context = HttpContext.Current;
                if (context == null)
                {
                    InstrumentationEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                    return;
                }

                var response = context.Response;
                activity.AddTag(SpanAttributeConstants.HttpStatusCodeKey, response.StatusCode.ToString());
                Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode);
                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, response.StatusDescription);

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
                    // Override the name that was previously set to the path part of URL.
                    activity.DisplayName = template;
                    activity.AddTag(SpanAttributeConstants.HttpRouteKey, template);
                }
            }
        }
    }
}
