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
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class GrpcClientDiagnosticListener : ListenerHandler
    {
        private static readonly Regex GrpcMethodRegex = new Regex(@"(?<package>\w+).(?<service>\w+)/(?<method>\w+)", RegexOptions.Compiled);

        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");

        public GrpcClientDiagnosticListener(Tracer tracer)
            : base("Grpc.Net.Client", tracer)
        {
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

            this.Tracer.StartActiveSpanFromActivity(grpcMethod, activity, SpanKind.Client, out var span);

            if (span.IsRecording)
            {
                var rpcService = GrpcMethodRegex.Match(grpcMethod).Groups["service"].Value;

                span.SetAttribute("rpc.service", rpcService);

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    span.SetAttribute("net.peer.ip", request.RequestUri.Host);
                }
                else
                {
                    span.SetAttribute("net.peer.name", request.RequestUri.Host);
                }

                span.SetAttribute("net.peer.port", request.RequestUri.Port);
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;
            try
            {
                if (span == null || !span.Context.IsValid)
                {
                    InstrumentationEventSource.Log.NullOrBlankSpan(nameof(GrpcClientDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (span.IsRecording)
                {
                    span.Status = Status.Unknown;

                    var grpcStatusCodeTag = activity.Tags.FirstOrDefault(tag => tag.Key == "grpc.status_code").Value;
                    if (int.TryParse(grpcStatusCodeTag, out var statusCode))
                    {
                        span.PutRpcStatusCode(statusCode);
                    }
                }
            }
            finally
            {
                span?.End();
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnException";
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                InstrumentationEventSource.Log.NullOrBlankSpan(nameof(GrpcClientDiagnosticListener) + EventNameSuffix);
                return;
            }

            // set span.Status
            // validate whether we need to end span here
        }
    }
}
