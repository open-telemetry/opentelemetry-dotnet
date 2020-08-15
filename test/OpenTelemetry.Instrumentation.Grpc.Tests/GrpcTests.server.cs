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
using System.Linq;
using System.Net;
using System.Threading;
using Greet;
using Grpc.Net.Client;
using Moq;
using OpenTelemetry.Instrumentation.Grpc.Tests.Services;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public partial class GrpcTests : IClassFixture<GrpcFixture<GreeterService>>
    {
        private GrpcFixture<GreeterService> fixture;

        public GrpcTests(GrpcFixture<GreeterService> fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes()
        {
            var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
            var uri = new Uri($"http://localhost:{this.fixture.Port}");
            var spanProcessor = this.fixture.GrpcServerSpanProcessor;

            var channel = GrpcChannel.ForAddress(uri);
            var client = new Greeter.GreeterClient(channel);
            var rs = client.SayHello(new HelloRequest());

            WaitForProcessorInvocations(spanProcessor, 2);

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("grpc", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcSystem).Value);
            Assert.Equal("greet.Greeter", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcService).Value);
            Assert.Equal("SayHello", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcMethod).Value);
            Assert.Contains(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerIp).Value, clientLoopbackAddresses);
            Assert.True(!string.IsNullOrEmpty(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerPort).Value));
            Assert.Equal(Status.Ok, span.GetStatus());

            // The following are http.* attributes that are also included on the span for the gRPC invocation.
            Assert.Equal($"localhost:{this.fixture.Port}", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpHost).Value);
            Assert.Equal("POST", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpMethod).Value);
            Assert.Equal("/greet.Greeter/SayHello", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
            Assert.Equal($"http://localhost:{this.fixture.Port}/greet.Greeter/SayHello", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpUrl).Value);
            Assert.StartsWith("grpc-dotnet", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpUserAgent).Value);

            // This attribute is added by the gRPC for .NET library. There is a discussion of having the OpenTelemetry instrumentation remove it.
            // See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/482#issuecomment-655753756
            Assert.Equal($"0", span.Tags.FirstOrDefault(i => i.Key == "grpc.status_code").Value);
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
