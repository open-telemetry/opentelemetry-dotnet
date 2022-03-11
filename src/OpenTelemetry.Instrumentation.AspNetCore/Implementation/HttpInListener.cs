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
using System.Reflection;
#if !NETSTANDARD2_0
using System.Runtime.CompilerServices;
#endif
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
#if !NETSTANDARD2_0
using OpenTelemetry.Instrumentation.GrpcNetClient;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    internal class HttpInListener : ListenerHandler
    {
        internal const string ActivityOperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
        internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
        private const string DiagnosticSourceName = "Microsoft.AspNetCore";
        private const string UnknownHostName = "UNKNOWN-HOST";
        private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers[name];
        private readonly PropertyFetcher<HttpContext> startContextFetcher = new("HttpContext");
        private readonly PropertyFetcher<HttpContext> stopContextFetcher = new("HttpContext");
        private readonly PropertyFetcher<Exception> stopExceptionFetcher = new("Exception");
        private readonly PropertyFetcher<object> beforeActionActionDescriptorFetcher = new("actionDescriptor");
        private readonly PropertyFetcher<object> beforeActionAttributeRouteInfoFetcher = new("AttributeRouteInfo");
        private readonly PropertyFetcher<string> beforeActionTemplateFetcher = new("Template");
        private readonly AspNetCoreInstrumentationOptions options;

        public HttpInListener(AspNetCoreInstrumentationOptions options)
            : base(DiagnosticSourceName)
        {
            Guard.ThrowIfNull(options);

            this.options = options;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public override void OnStartActivity(Activity activity, object payload)
        {
            // The overall flow of what AspNetCore library does is as below:
            // Activity.Start()
            // DiagnosticSource.WriteEvent("Start", payload)
            // DiagnosticSource.WriteEvent("Stop", payload)
            // Activity.Stop()

            // This method is in the WriteEvent("Start", payload) path.
            // By this time, samplers have already run and
            // activity.IsAllDataRequested populated accordingly.

            if (Sdk.SuppressInstrumentation)
            {
                return;
            }

            _ = this.startContextFetcher.TryFetch(payload, out HttpContext context);
            if (context == null)
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStartActivity));
                return;
            }

            // Ensure context extraction irrespective of sampling decision
            var request = context.Request;
            var textMapPropagator = Propagators.DefaultTextMapPropagator;
            if (textMapPropagator is not TraceContextPropagator)
            {
                var ctx = textMapPropagator.Extract(default, request, HttpRequestHeaderValuesGetter);

                if (ctx.ActivityContext.IsValid()
                    && ctx.ActivityContext != new ActivityContext(activity.TraceId, activity.ParentSpanId, activity.ActivityTraceFlags, activity.TraceStateString, true))
                {
                    // Create a new activity with its parent set from the extracted context.
                    // This makes the new activity as a "sibling" of the activity created by
                    // Asp.Net Core.
                    Activity newOne = new Activity(ActivityOperationName);
                    newOne.SetParentId(ctx.ActivityContext.TraceId, ctx.ActivityContext.SpanId, ctx.ActivityContext.TraceFlags);
                    newOne.TraceStateString = ctx.ActivityContext.TraceState;

                    newOne.SetTag("IsCreatedByInstrumentation", bool.TrueString);

                    // Starting the new activity make it the Activity.Current one.
                    newOne.Start();

                    // Set IsAllDataRequested to false for the activity created by the framework to only export the sibling activity and not the framework activity
                    activity.IsAllDataRequested = false;
                    activity = newOne;
                }

                Baggage.Current = ctx.Baggage;
            }

            // enrich Activity from payload only if sampling decision
            // is favorable.
            if (activity.IsAllDataRequested)
            {
                try
                {
                    if (this.options.Filter?.Invoke(context) == false)
                    {
                        AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                        activity.IsAllDataRequested = false;
                        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.RequestFilterException(ex);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }

                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Server);

                var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
                activity.DisplayName = path;

                // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md

                if (request.Host.Port is null or 80 or 443)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Host.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Host.Host + ":" + request.Host.Port);
                }

                activity.SetTag(SemanticConventions.AttributeHttpMethod, request.Method);
                activity.SetTag(SemanticConventions.AttributeHttpTarget, path);
                activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUri(request));

                var userAgent = request.Headers["User-Agent"].FirstOrDefault();
                if (!string.IsNullOrEmpty(userAgent))
                {
                    activity.SetTag(SemanticConventions.AttributeHttpUserAgent, userAgent);
                }

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                _ = this.stopContextFetcher.TryFetch(payload, out HttpContext context);
                if (context == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStopActivity));
                    return;
                }

                var response = context.Response;

                activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

#if !NETSTANDARD2_0
                if (this.options.EnableGrpcAspNetCoreSupport && TryGetGrpcMethod(activity, out var grpcMethod))
                {
                    AddGrpcAttributes(activity, grpcMethod, context);
                }
                else if (activity.GetStatus().StatusCode == StatusCode.Unset)
                {
                    activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, response.StatusCode));
                }
#else
                if (activity.GetStatus().StatusCode == StatusCode.Unset)
                {
                    activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, response.StatusCode));
                }
#endif

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStopActivity", response);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            if (activity.TryCheckFirstTag("IsCreatedByInstrumentation", out var tagValue) && ReferenceEquals(tagValue, bool.TrueString))
            {
                // If instrumentation started a new Activity, it must
                // be stopped here.
                activity.SetTag("IsCreatedByInstrumentation", null);
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

            var textMapPropagator = Propagators.DefaultTextMapPropagator;
            if (textMapPropagator is not TraceContextPropagator)
            {
                Baggage.Current = default;
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
                    _ = this.beforeActionActionDescriptorFetcher.TryFetch(payload, out var actionDescriptor);
                    _ = this.beforeActionAttributeRouteInfoFetcher.TryFetch(actionDescriptor, out var attributeRouteInfo);
                    _ = this.beforeActionTemplateFetcher.TryFetch(attributeRouteInfo, out var template);

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

        public override void OnException(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                if (!this.stopExceptionFetcher.TryFetch(payload, out Exception exc) || exc == null)
                {
                    AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnException));
                    return;
                }

                if (this.options.RecordException)
                {
                    activity.RecordException(exc);
                }

                activity.SetStatus(Status.Error.WithDescription(exc.Message));

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnException", exc);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        private static string GetUri(HttpRequest request)
        {
            // this follows the suggestions from https://github.com/dotnet/aspnetcore/issues/28906
            var scheme = request.Scheme ?? string.Empty;

            // HTTP 1.0 request with NO host header would result in empty Host.
            // Use placeholder to avoid incorrect URL like "http:///"
            var host = request.Host.Value ?? UnknownHostName;
            var pathBase = request.PathBase.Value ?? string.Empty;
            var path = request.Path.Value ?? string.Empty;
            var queryString = request.QueryString.Value ?? string.Empty;
            var length = scheme.Length + Uri.SchemeDelimiter.Length + host.Length + pathBase.Length
                         + path.Length + queryString.Length;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            return string.Create(length, (scheme, host, pathBase, path, queryString), (span, parts) =>
            {
                CopyTo(ref span, parts.scheme);
                CopyTo(ref span, Uri.SchemeDelimiter);
                CopyTo(ref span, parts.host);
                CopyTo(ref span, parts.pathBase);
                CopyTo(ref span, parts.path);
                CopyTo(ref span, parts.queryString);

                static void CopyTo(ref Span<char> buffer, ReadOnlySpan<char> text)
                {
                    if (!text.IsEmpty)
                    {
                        text.CopyTo(buffer);
                        buffer = buffer.Slice(text.Length);
                    }
                }
            });
#else
            return new System.Text.StringBuilder(length)
                .Append(scheme)
                .Append(Uri.SchemeDelimiter)
                .Append(host)
                .Append(pathBase)
                .Append(path)
                .Append(queryString)
                .ToString();
#endif
        }

#if !NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetGrpcMethod(Activity activity, out string grpcMethod)
        {
            grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);
            return !string.IsNullOrEmpty(grpcMethod);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddGrpcAttributes(Activity activity, string grpcMethod, HttpContext context)
        {
            // The RPC semantic conventions indicate the span name
            // should not have a leading forward slash.
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/rpc.md#span-name
            activity.DisplayName = grpcMethod.TrimStart('/');

            activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);
            activity.SetTag(SemanticConventions.AttributeNetPeerIp, context.Connection.RemoteIpAddress.ToString());
            activity.SetTag(SemanticConventions.AttributeNetPeerPort, context.Connection.RemotePort);

            bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
            if (validConversion)
            {
                activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status));
            }

            if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
            {
                activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

                // Remove the grpc.method tag added by the gRPC .NET library
                activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);

                // Remove the grpc.status_code tag added by the gRPC .NET library
                activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

                if (validConversion)
                {
                    // setting rpc.grpc.status_code
                    activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);
                }
            }
        }
#endif
    }
}
