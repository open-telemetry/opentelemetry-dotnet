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
    internal sealed class HttpInListener : IDisposable
    {
        private readonly PropertyFetcher<object> routeFetcher = new PropertyFetcher<object>("Route");
        private readonly PropertyFetcher<string> routeTemplateFetcher = new PropertyFetcher<string>("RouteTemplate");
        private readonly AspNetInstrumentationOptions options;

        public HttpInListener(AspNetInstrumentationOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            TelemetryHttpModule.Options.TextMapPropagator = Propagators.DefaultTextMapPropagator;

            TelemetryHttpModule.Options.OnRequestStartedCallback += this.OnStartActivity;
            TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
            TelemetryHttpModule.Options.OnExceptionCallback += this.OnException;
        }

        public void Dispose()
        {
            TelemetryHttpModule.Options.OnRequestStartedCallback -= this.OnStartActivity;
            TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
            TelemetryHttpModule.Options.OnExceptionCallback -= this.OnException;
        }

        /// <summary>
        /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri"><see cref="Uri"/>.</param>
        /// <returns>Span uri value.</returns>
        private static string GetUriTagValueFromRequestUri(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.UserInfo))
            {
                return uri.ToString();
            }

            return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.PathAndQuery, uri.Fragment);
        }

        private void OnStartActivity(Activity activity, HttpContext context)
        {
            if (activity.IsAllDataRequested)
            {
                try
                {
                    // todo: Ideally we would also check
                    // Sdk.SuppressInstrumentation here to prevent tagging a
                    // span that will not be collected but we can't do that
                    // without an SDK reference. Need the spec to come around on
                    // this.

                    if (this.options.Filter?.Invoke(context) == false)
                    {
                        AspNetInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                        activity.IsAllDataRequested = false;
                        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.RequestFilterException(activity.OperationName, ex);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }

                var request = context.Request;
                var requestValues = request.Unvalidated;

                // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md
                var path = requestValues.Path;
                activity.DisplayName = path;

                if (request.Url.Port == 80 || request.Url.Port == 443)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host + ":" + request.Url.Port);
                }

                activity.SetTag(SemanticConventions.AttributeHttpMethod, request.HttpMethod);
                activity.SetTag(SemanticConventions.AttributeHttpTarget, path);
                activity.SetTag(SemanticConventions.AttributeHttpUserAgent, request.UserAgent);
                activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUriTagValueFromRequestUri(request.Url));

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.EnrichmentException("OnStartActivity", ex);
                }
            }
        }

        private void OnStopActivity(Activity activity, HttpContext context)
        {
            if (activity.IsAllDataRequested)
            {
                var response = context.Response;

                activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

                if (activity.GetStatus().StatusCode == StatusCode.Unset)
                {
                    activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(response.StatusCode));
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
                    activity.DisplayName = template;
                    activity.SetTag(SemanticConventions.AttributeHttpRoute, template);
                }

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStopActivity", response);
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.EnrichmentException("OnStopActivity", ex);
                }
            }
        }

        private void OnException(Activity activity, HttpContext context, Exception exception)
        {
            if (activity.IsAllDataRequested)
            {
                if (this.options.RecordException)
                {
                    activity.RecordException(exception);
                }

                activity.SetStatus(Status.Error.WithDescription(exception.Message));

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnException", exception);
                }
                catch (Exception ex)
                {
                    AspNetInstrumentationEventSource.Log.EnrichmentException("OnException", ex);
                }
            }
        }
    }
}
