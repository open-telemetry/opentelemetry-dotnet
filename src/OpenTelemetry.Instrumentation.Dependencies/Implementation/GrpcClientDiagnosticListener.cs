// <copyright file="GrpcClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class GrpcClientDiagnosticListener : ListenerHandler
    {
        // The Grpc.Net.Client library adds its own tags to the activity.
        // These tags are used to source the tags added by the OpenTelemetry instrumentation.
        private const string GrpcMethodTagName = "grpc.method";
        private const string GrpcStatusCodeTagName = "grpc.status_code";

        private static readonly Regex GrpcMethodRegex = new Regex(@"(?<service>\w+\.?\w*)/(?<method>\w+)", RegexOptions.Compiled);
        private static readonly PropertyInfo ActivityKindPropertyInfo = typeof(Activity).GetProperty("Kind");

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");

        public GrpcClientDiagnosticListener(ActivitySourceAdapter activitySource)
            : base("Grpc.Net.Client", null)
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                InstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener) + EventNameSuffix);
                return;
            }

            var grpcMethodTag = activity.Tags.FirstOrDefault(tag => tag.Key == GrpcMethodTagName);
            var grpcMethod = grpcMethodTag.Value?.Trim('/');

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            ActivityKindPropertyInfo.SetValue(activity, ActivityKind.Client);
            activity.DisplayName = grpcMethod;

            this.activitySource.Start(activity);

            if (activity.IsAllDataRequested)
            {
                activity.AddTag("rpc.system", "grpc");

                var match = GrpcMethodRegex.Match(grpcMethod);
                if (match.Success)
                {
                    var rpcService = match.Groups["service"].Value;
                    if (!string.IsNullOrEmpty(rpcService))
                    {
                        activity.AddTag("rpc.service", rpcService);
                    }

                    var rpcMethod = match.Groups["method"].Value;
                    if (!string.IsNullOrEmpty(rpcMethod))
                    {
                        activity.AddTag("rpc.method", rpcMethod);
                    }
                }

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.AddTag("net.peer.ip", request.RequestUri.Host);
                }
                else
                {
                    activity.AddTag("net.peer.name", request.RequestUri.Host);
                }

                activity.AddTag("net.peer.port", request.RequestUri.Port.ToString());
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                var status = Status.Unknown;

                var grpcStatusCodeTag = activity.Tags.FirstOrDefault(tag => tag.Key == GrpcStatusCodeTagName).Value;
                if (int.TryParse(grpcStatusCodeTag, out var statusCode))
                {
                    status = SpanHelper.ResolveSpanStatusForGrpcStatusCode(statusCode);
                }

                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
            }

            this.activitySource.Stop(activity);
        }
    }
}
