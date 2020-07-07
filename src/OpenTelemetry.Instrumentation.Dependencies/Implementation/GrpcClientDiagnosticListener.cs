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
using System.Net.Http;
using System.Reflection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class GrpcClientDiagnosticListener : ListenerHandler
    {
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

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
            ActivityKindPropertyInfo.SetValue(activity, ActivityKind.Client);
            activity.DisplayName = grpcMethod?.Trim('/');

            this.activitySource.Start(activity);

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SpanAttributeConstants.RpcSystem, "grpc");

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.AddTag(SpanAttributeConstants.RpcService, rpcService);
                    activity.AddTag(SpanAttributeConstants.RpcMethod, rpcMethod);
                }

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.AddTag(SpanAttributeConstants.NetPeerIp, request.RequestUri.Host);
                }
                else
                {
                    activity.AddTag(SpanAttributeConstants.NetPeerName, request.RequestUri.Host);
                }

                activity.AddTag(SpanAttributeConstants.NetPeerPort, request.RequestUri.Port.ToString());
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SpanAttributeConstants.StatusCodeKey, GrpcTagHelper.GetGrpcStatusCodeFromActivity(activity));
            }

            this.activitySource.Stop(activity);
        }
    }
}
