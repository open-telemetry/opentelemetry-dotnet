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

namespace OpenTelemetry.Instrumentation.GrpcNetClient.Implementation
{
    internal class GrpcClientDiagnosticListener : ListenerHandler
    {
        public const string RequestCustomPropertyName = "OTel.GrpcHandler.Request";
        private readonly GrpcClientInstrumentationOptions options;

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher<HttpRequestMessage> startRequestFetcher = new PropertyFetcher<HttpRequestMessage>("Request");

        public GrpcClientDiagnosticListener(ActivitySourceAdapter activitySource, GrpcClientInstrumentationOptions options)
            : base("Grpc.Net.Client")
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.options = options;
            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                GrpcInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            activity.DisplayName = grpcMethod?.Trim('/');

            this.activitySource.Start(activity, ActivityKind.Client);

            if (activity.IsAllDataRequested)
            {
                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
                }

                activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                    activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

                    // Remove the grpc.method tag added by the gRPC .NET library
                    activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);
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

                activity.SetTag(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port);
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
