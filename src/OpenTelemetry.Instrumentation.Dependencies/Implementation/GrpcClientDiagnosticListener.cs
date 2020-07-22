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
                DependenciesInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            activity.SetKind(ActivityKind.Client);
            activity.DisplayName = grpcMethod?.Trim('/');

            this.activitySource.Start(activity);

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SemanticConventions.AttributeRPCSystem, "grpc");

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.AddTag(SemanticConventions.AttributeRPCService, rpcService);
                    activity.AddTag(SemanticConventions.AttributeRPCMethod, rpcMethod);
                }

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.AddTag(SemanticConventions.AttributeNetPeerIP, request.RequestUri.Host);
                }
                else
                {
                    activity.AddTag(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host);
                }

                activity.AddTag(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port.ToString());
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
