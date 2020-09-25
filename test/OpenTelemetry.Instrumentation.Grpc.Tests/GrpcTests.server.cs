// <copyright file="GrpcTests.server.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Greet;
using Grpc.Net.Client;
using Moq;
using OpenTelemetry.Instrumentation.Grpc.Tests.Services;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public partial class GrpcTests : IClassFixture<GrpcFixture<GreeterService>>
    {
        private readonly GrpcFixture<GreeterService> fixture;

        public GrpcTests(GrpcFixture<GreeterService> fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes()
        {
            var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
            var uri = new Uri($"http://localhost:{this.fixture.Port}");
            var processor = this.fixture.GrpcServerSpanProcessor;

            var channel = GrpcChannel.ForAddress(uri);
            var client = new Greeter.GreeterClient(channel);
            client.SayHello(new HelloRequest());

            WaitForProcessorInvocations(processor, 2);

            Assert.Equal(2, processor.Invocations.Count); // begin and end was called
            var activity = (Activity)processor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeNetPeerIp), clientLoopbackAddresses);
            Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Equal(Status.Ok, activity.GetStatus());

            // The following are http.* attributes that are also included on the span for the gRPC invocation.
            Assert.Equal($"localhost:{this.fixture.Port}", activity.GetTagValue(SemanticConventions.AttributeHttpHost));
            Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
            Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SpanAttributeConstants.HttpPathKey));
            Assert.Equal($"http://localhost:{this.fixture.Port}/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
            Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);

            // Tags added by the library then removed from the instrumentation
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        private static void WaitForProcessorInvocations(Mock<ActivityProcessor> spanProcessor, int invocationCount)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return spanProcessor.Invocations.Count >= invocationCount;
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
