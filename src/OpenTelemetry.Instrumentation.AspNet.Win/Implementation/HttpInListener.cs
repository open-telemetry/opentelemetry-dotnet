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

namespace OpenTelemetry.Instrumentation.AspNet.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        private static readonly string ActivityNameByHttpInListener = "ActivityCreatedByHttpInListener";
        private readonly PropertyFetcher routeFetcher = new PropertyFetcher("Route");
        private readonly PropertyFetcher routeTemplateFetcher = new PropertyFetcher("RouteTemplate");
        private readonly AspNetInstrumentationOptions options;
        private readonly ActivitySourceAdapter activitySource;

        public HttpInListener(string name, AspNetInstrumentationOptions options, ActivitySourceAdapter activitySource)
            : base(name, null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.activitySource = activitySource;
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
                InstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                activity.IsAllDataRequested = false;
                return;
            }

            var request = context.Request;
            var requestValues = request.Unvalidated;

            if (!(this.options.TextFormat is TraceContextFormatActivity))
            {
                // This requires to ignore the current activity and create a new one
                // using the context extracted using the format TextFormat supports.
                var ctx = this.options.TextFormat.Extract<HttpRequest>(
                    request,
                    (r, name) => requestValues.Headers.GetValues(name));

                // Create a new activity with its parent set from the extracted context.
                // This makes the new activity as a "sibling" of the activity created by
                // Asp.Net.
                Activity newOne = new Activity(ActivityNameByHttpInListener);
                newOne.SetParentId(ctx.TraceId, ctx.SpanId, ctx.TraceFlags);
                newOne.TraceStateString = ctx.TraceState;

                // Starting the new activity make it the Activity.Current one.
                newOne.Start();

                // Both new activity and old one store the other activity
                // inside them. This is required in the Stop step to
                // correctly stop and restore Activity.Current.
                newOne.SetCustomProperty("ActivityByAspNet", activity);
                activity.SetCustomProperty("ActivityByHttpInListener", newOne);
                activity = newOne;
            }

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md
            var path = requestValues.Path;
            activity.DisplayName = path;

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            activity.GetType().GetProperty("Kind").SetValue(activity, ActivityKind.Server);

            this.activitySource.Start(activity);

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

            Activity activityToEnrich = activity;

            if (!(this.options.TextFormat is TraceContextFormatActivity))
            {
                // If using custom context propagator, then the activity here
                // could be either the one from Asp.Net, or the one
                // this instrumentation created in Start.
                // This is because Asp.Net, under certain circumstances, restores Activity.Current
                // to its own activity.
                if (activity.OperationName.Equals("Microsoft.AspNet.HttpReqIn.Start"))
                {
                    // This block is hit if Asp.Net did restore Current to its own activity,
                    // then we need to retrieve the one created by HttpInListener
                    // and populate tags to it.
                    activityToEnrich = (Activity)activity.GetCustomProperty("ActivityByHttpInListener");
                }
            }

            if (activityToEnrich.IsAllDataRequested)
            {
                var context = HttpContext.Current;
                if (context == null)
                {
                    InstrumentationEventSource.Log.NullPayload(nameof(HttpInListener) + EventNameSuffix);
                    return;
                }

                var response = context.Response;
                activityToEnrich.AddTag(SpanAttributeConstants.HttpStatusCodeKey, response.StatusCode.ToString());
                Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode);
                activityToEnrich.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                activityToEnrich.AddTag(SpanAttributeConstants.StatusDescriptionKey, response.StatusDescription);

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
                    activityToEnrich.DisplayName = template;
                    activityToEnrich.AddTag(SpanAttributeConstants.HttpRouteKey, template);
                }
            }

            if (!(this.options.TextFormat is TraceContextFormatActivity))
            {
                if (activity.OperationName.Equals(ActivityNameByHttpInListener))
                {
                    // If instrumentation started a new Activity, it must
                    // be stopped here.
                    activity.Stop();

                    // Restore the original activity as Current.
                    var activityByAspNet = (Activity)activity.GetCustomProperty("ActivityByAspNet");
                    Activity.Current = activityByAspNet;
                }
                else if (activity.OperationName.Equals("Microsoft.AspNet.HttpReqIn.Start"))
                {
                    // This block is hit if Asp.Net did restore Current to its own activity,
                    // then we need to retrieve the one created by HttpInListener
                    // and stop it.
                    var activityByHttpInListener = (Activity)activity.GetCustomProperty("ActivityByHttpInListener");
                    activityByHttpInListener.Stop();

                    // Restore current back to the one created by Asp.Net
                    Activity.Current = activity;
                }
            }

            this.activitySource.Stop(activityToEnrich);
        }
    }
}
