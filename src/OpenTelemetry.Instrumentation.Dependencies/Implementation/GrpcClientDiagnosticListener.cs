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
        private static readonly Regex GrpcMethodRegex = new Regex(@"((?<package>\w+)\.)?(?<service>\w+)/(?<method>\w+)", RegexOptions.Compiled);

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyInfo activityKindPropertyInfo = typeof(Activity).GetProperty("Kind");

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

            var grpcMethodTag = activity.Tags.FirstOrDefault(tag => tag.Key == "grpc.method");
            var grpcMethod = grpcMethodTag.Value?.Trim('/');

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            this.activityKindPropertyInfo.SetValue(activity, ActivityKind.Client);
            activity.DisplayName = grpcMethod;

            this.activitySource.Start(activity);

            if (activity.IsAllDataRequested)
            {
                var rpcService = GrpcMethodRegex.Match(grpcMethod).Groups["service"].Value;

                activity.AddTag("rpc.service", rpcService);

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

                var grpcStatusCodeTag = activity.Tags.FirstOrDefault(tag => tag.Key == "grpc.status_code").Value;
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
