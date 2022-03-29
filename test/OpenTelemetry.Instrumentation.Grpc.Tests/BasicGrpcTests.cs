// <copyright file="BasicGrpcTests.cs" company="OpenTelemetry Authors">
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
using System.Net;
using Greet;
using Grpc.Net.Client;
using Moq;
using OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Trace;
using Xunit;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public class BasicGrpcTests
    {
        [Fact]
        public void GrpcClientCallsWork()
        {
            var httpClient = ClientTestHelpers.CreateTestClient(async request =>
            {
                var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply()).DefaultTimeout();
                var response = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.OK);
                response.TrailingHeaders().Add("grpc-message", "value");
                return response;
            });

            var uri = new Uri($"http://localhost");
            var uriHostNameType = Uri.CheckHostName(uri.Host);

            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation()
                    .AddProcessor(processor.Object)
                    .Build())
            {
                var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions()
                {
                    HttpClient = httpClient,
                });
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

            ValidateGrpcActivity(activity);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.Equal($"greet.Greeter/SayHello", activity.DisplayName);
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));

            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
                Assert.Null(activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            }
            else
            {
                Assert.Null(activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
                Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            }

            Assert.Equal(uri.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Equal(Status.Unset, activity.GetStatus());

            // Tags added by the library then removed from the instrumentation
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }

        private static void ValidateGrpcActivity(Activity activityToValidate)
        {
            Assert.Equal(GrpcClientDiagnosticListener.ActivitySourceName, activityToValidate.Source.Name);
            Assert.Equal(GrpcClientDiagnosticListener.Version.ToString(), activityToValidate.Source.Version);
            Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
        }
    }
}
