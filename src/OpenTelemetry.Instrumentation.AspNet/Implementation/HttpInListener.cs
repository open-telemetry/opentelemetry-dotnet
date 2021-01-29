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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        internal const string ActivityNameByHttpInListener = "ActivityCreatedByHttpInListener";
        internal const string ActivityOperationName = "Microsoft.AspNet.HttpReqIn";
        internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
        private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers.GetValues(name);
        private readonly PropertyFetcher<object> routeFetcher = new PropertyFetcher<object>("Route");
        private readonly PropertyFetcher<string> routeTemplateFetcher = new PropertyFetcher<string>("RouteTemplate");
        private readonly AspNetInstrumentationOptions options;
        private readonly ActivitySourceAdapter activitySource;

        public HttpInListener(string name, AspNetInstrumentationOptions options, ActivitySourceAdapter activitySource)
            : base(name)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.activitySource = activitySource;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Activity is retrieved from Activity.Current later and disposed.")]
        public override void OnStartActivity(Activity activity, object payload)
        {
            var context = HttpContext.Current;
            if (context == null)
            {
                AspNetInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStartActivity));
                return;
            }

            try
            {
                if (this.options.Filter?.Invoke(context) == false)
                {
                    AspNetInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.RequestFilterException(ex);
                activity.IsAllDataRequested = false;
                return;
            }

            var request = context.Request;
            var requestValues = request.Unvalidated;
            var textMapPropagator = Propagators.DefaultTextMapPropagator;

            if (!(textMapPropagator is TraceContextPropagator))
            {
                var ctx = textMapPropagator.Extract(default, request, HttpRequestHeaderValuesGetter);

                if (ctx.ActivityContext.IsValid()
                    && ctx.ActivityContext != new ActivityContext(activity.TraceId, activity.ParentSpanId, activity.ActivityTraceFlags, activity.TraceStateString, true))
                {
                    // Create a new activity with its parent set from the extracted context.
                    // This makes the new activity as a "sibling" of the activity created by
                    // ASP.NET.
                    Activity newOne = new Activity(ActivityNameByHttpInListener);
                    newOne.SetParentId(ctx.ActivityContext.TraceId, ctx.ActivityContext.SpanId, ctx.ActivityContext.TraceFlags);
                    newOne.TraceStateString = ctx.ActivityContext.TraceState;

                    // Starting the new activity make it the Activity.Current one.
                    newOne.Start();

                    // Both new activity and old one store the other activity
                    // inside them. This is required in the Stop step to
                    // correctly stop and restore Activity.Current.
                    newOne.SetCustomProperty("OTel.ActivityByAspNet", activity);
                    activity.SetCustomProperty("OTel.ActivityByHttpInListener", newOne);
                    activity = newOne;
                }

                if (ctx.Baggage != default)
                {
                    Baggage.Current = ctx.Baggage;
                }
            }

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/data-semantic-conventions.md
            var path = requestValues.Path;
            activity.DisplayName = path;

            this.activitySource.Start(activity, ActivityKind.Server, ActivitySource);

            if (activity.IsAllDataRequested)
            {
                if (request.Url.Port == 80 || request.Url.Port == 443)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host + ":" + request.Url.Port);
                }

                activity.SetTag(SemanticConventions.AttributeHttpMethod, request.HttpMethod);
                activity.SetTag(SpanAttributeConstants.HttpPathKey, path);
                activity.SetTag(SemanticConventions.AttributeHttpUserAgent, request.UserAgent);
                activity.SetTag(SemanticConventions.AttributeHttpUrl, request.Url.ToString());

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            Activity activityToEnrich = activity;
            Activity createdActivity = null;

            var textMapPropagator = Propagators.DefaultTextMapPropagator;
            bool isCustomPropagator = !(textMapPropagator is TraceContextPropagator);

            if (isCustomPropagator)
            {
                // If using custom context propagator, then the activity here
                // could be either the one from Asp.Net, or the one
                // this instrumentation created in Start.
                // This is because Asp.Net, under certain circumstances, restores Activity.Current
                // to its own activity.
                if (activity.OperationName.Equals(ActivityOperationName, StringComparison.Ordinal))
                {
                    // This block is hit if Asp.Net did restore Current to its own activity,
                    // and we need to retrieve the one created by HttpInListener,
                    // or an additional activity was never created.
                    createdActivity = (Activity)activity.GetCustomProperty("OTel.ActivityByHttpInListener");
                    activityToEnrich = createdActivity ?? activity;
                }
            }

            if (activityToEnrich.IsAllDataRequested)
            {
                var context = HttpContext.Current;
                if (context == null)
                {
                    AspNetInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStopActivity));
                    return;
                }

                var response = context.Response;

                activityToEnrich.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

                if (activityToEnrich.GetStatus().StatusCode == StatusCode.Unset)
                {
                    activityToEnrich.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(response.StatusCode));
                }

                var routeData = context.Request.RequestContext.RouteData;

                string template = null;
                if (routeData.Values.TryGetValue("MS_SubRoutes", out object msSubRoutes))
                {
                    // WebAPI attribute routing flows here. Use reflection to not take a dependency on microsoft.aspnet.webapi.core\[version]\lib\[framework]\System.Web.Http.

                    if (msSubRoutes is Array attributeRouting && attributeRouting.Length == 1)
                    {
                        var subRouteData = attributeRouting.GetValue(0);

                        _ = this.routeFetcher.TryFetch(subRouteData, out var route);
                        _ = this.routeTemplateFetcher.TryFetch(route, out template);
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
                    activityToEnrich.SetTag(SemanticConventions.AttributeHttpRoute, template);
                }

                try
                {
                    this.options.Enrich?.Invoke(activityToEnrich, "OnStopActivity", response);
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            if (isCustomPropagator)
            {
                if (activity.OperationName.Equals(ActivityNameByHttpInListener, StringComparison.Ordinal))
                {
                    // If instrumentation started a new Activity, it must
                    // be stopped here.
                    activity.Stop();

                    // Restore the original activity as Current.
                    var activityByAspNet = (Activity)activity.GetCustomProperty("OTel.ActivityByAspNet");
                    Activity.Current = activityByAspNet;
                }
                else if (createdActivity != null)
                {
                    // This block is hit if Asp.Net did restore Current to its own activity,
                    // then we need to retrieve the one created by HttpInListener
                    // and stop it.
                    createdActivity.Stop();

                    // Restore current back to the one created by Asp.Net
                    Activity.Current = activity;
                }
            }

            this.activitySource.Stop(activityToEnrich);
        }
    }
}
