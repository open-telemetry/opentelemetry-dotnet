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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Grpc.Implementation
{
    internal class GrpcClientDiagnosticListener : ListenerHandler
    {
        public const string RequestCustomPropertyName = "OTel.GrpcHandler.Request";

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");

        public GrpcClientDiagnosticListener(ActivitySourceAdapter activitySource)
            : base("Grpc.Net.Client")
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                GrpcInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            activity.SetKind(ActivityKind.Client);
            activity.DisplayName = grpcMethod?.Trim('/');
            activity.SetCustomProperty(RequestCustomPropertyName, request);

            this.activitySource.Start(activity);

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                    activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);
                }

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerIp, request.RequestUri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host);
                }

                activity.SetTag(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port.ToString());
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                activity.SetStatus(GrpcTagHelper.GetGrpcStatusCodeFromActivity(activity));
            }

            this.activitySource.Stop(activity);
        }
    }
}
