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
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        public const string RequestCustomPropertyName = "OTel.AspNetCore.Request";
        public const string ResponseCustomPropertyName = "OTel.AspNetCore.Response";
        private const string UnknownHostName = "UNKNOWN-HOST";
        private const string ActivityNameByHttpInListener = "ActivityCreatedByHttpInListener";
        private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers[name];
        private readonly PropertyFetcher<HttpContext> startContextFetcher = new PropertyFetcher<HttpContext>("HttpContext");
        private readonly PropertyFetcher<HttpContext> stopContextFetcher = new PropertyFetcher<HttpContext>("HttpContext");
        private readonly PropertyFetcher<object> beforeActionActionDescriptorFetcher = new PropertyFetcher<object>("actionDescriptor");
        private readonly PropertyFetcher<object> beforeActionAttributeRouteInfoFetcher = new PropertyFetcher<object>("AttributeRouteInfo");
        private readonly PropertyFetcher<string> beforeActionTemplateFetcher = new PropertyFetcher<string>("Template");
        private readonly bool hostingSupportsW3C;
        private readonly AspNetCoreInstrumentationOptions options;
        private readonly ActivitySourceAdapter activitySource;

        public HttpInListener(string name, AspNetCoreInstrumentationOptions options, ActivitySourceAdapter activitySource)
            : base(name)
        {
            this.hostingSupportsW3C = typeof(HttpRequest).Assembly.GetName().Version.Major >= 3;
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.activitySource = activitySource;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public override void OnStartActivity(Activity activity, object payload)
        {
            HttpContext context = this.startContextFetcher.Fetch(payload);
            if (context == null)
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStartActivity));
                return;
            }

            try
            {
                if (this.options.Filter?.Invoke(context) == false)
                {
                    AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                AspNetCoreInstrumentationEventSource.Log.RequestFilterException(ex);
                activity.IsAllDataRequested = false;
                return;
            }

            var request = context.Request;
            if (!this.hostingSupportsW3C || !(this.options.Propagator is TextMapPropagator))
            {
                var ctx = this.options.Propagator.Extract(default, request, HttpRequestHeaderValuesGetter);

                if (ctx.ActivityContext.IsValid() && ctx.ActivityContext != activity.Context)
                {
                    // Create a new activity with its parent set from the extracted context.
                    // This makes the new activity as a "sibling" of the activity created by
                    // Asp.Net Core.
                    Activity newOne = new Activity(ActivityNameByHttpInListener);
                    newOne.SetParentId(ctx.ActivityContext.TraceId, ctx.ActivityContext.SpanId, ctx.ActivityContext.TraceFlags);
                    newOne.TraceStateString = ctx.ActivityContext.TraceState;

                    // Starting the new activity make it the Activity.Current one.
                    newOne.Start();
                    activity = newOne;
                }

                if (ctx.Baggage != default)
                {
                    Baggage.Current = ctx.Baggage;
                }
            }

            this.activitySource.Start(activity, ActivityKind.Server);

            if (activity.IsAllDataRequested)
            {
                activity.SetCustomProperty(RequestCustomPropertyName, request);

                var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
                activity.DisplayName = path;

                // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-semantic-conventions.md

                if (request.Host.Port == 80 || request.Host.Port == 443)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Host.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Host.Host + ":" + request.Host.Port);
                }

                activity.SetTag(SemanticConventions.AttributeHttpMethod, request.Method);
                activity.SetTag(SpanAttributeConstants.HttpPathKey, path);
                activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUri(request));

                var userAgent = request.Headers["User-Agent"].FirstOrDefault();
                if (!string.IsNullOrEmpty(userAgent))
                {
                    activity.SetTag(SemanticConventions.AttributeHttpUserAgent, userAgent);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                HttpContext context = this.stopContextFetcher.Fetch(payload);
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStopActivity));
                    return;
                }

                var response = context.Response;
                activity.SetCustomProperty(ResponseCustomPropertyName, response);
                activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

                if (TryGetGrpcMethod(activity, out var grpcMethod))
                {
                    AddGrpcAttributes(activity, grpcMethod, context);
                }
                else
                {
                    Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode(response.StatusCode);
                    activity.SetStatus(status.WithDescription(response.HttpContext.Features.Get<IHttpResponseFeature>()?.ReasonPhrase));
                }
            }

            if (activity.OperationName.Equals(ActivityNameByHttpInListener))
            {
                // If instrumentation started a new Activity, it must
                // be stopped here.
                activity.Stop();

                // After the activity.Stop() code, Activity.Current becomes null.
                // If Asp.Net Core uses Activity.Current?.Stop() - it'll not stop the activity
                // it created.
                // Currently Asp.Net core does not use Activity.Current, instead it stores a
                // reference to its activity, and calls .Stop on it.

                // TODO: Should we still restore Activity.Current here?
                // If yes, then we need to store the asp.net core activity inside
                // the one created by the instrumentation.
                // And retrieve it here, and set it to Current.
            }

            this.activitySource.Stop(activity);
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
                    var template = this.beforeActionTemplateFetcher.Fetch(attributeRouteInfo);

                    if (!string.IsNullOrEmpty(template))
                    {
                        // override the span name that was previously set to the path part of URL.
                        activity.DisplayName = template;
                        activity.SetTag(SemanticConventions.AttributeHttpRoute, template);
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

        private static bool TryGetGrpcMethod(Activity activity, out string grpcMethod)
        {
            grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);
            return !string.IsNullOrEmpty(grpcMethod);
        }

        private static void AddGrpcAttributes(Activity activity, string grpcMethod, HttpContext context)
        {
            // TODO: Should the leading slash be trimmed? Spec seems to suggest no leading slash: https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/rpc.md#span-name
            // Client instrumentation is trimming the leading slash. Whatever we decide here, should we apply the same to the client side?
            // activity.DisplayName = grpcMethod?.Trim('/');

            activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

            if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
            {
                activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);
            }

            activity.SetTag(SemanticConventions.AttributeNetPeerIp, context.Connection.RemoteIpAddress.ToString());
            activity.SetTag(SemanticConventions.AttributeNetPeerPort, context.Connection.RemotePort);
            activity.SetStatus(GrpcTagHelper.GetGrpcStatusCodeFromActivity(activity));
        }
    }
}
