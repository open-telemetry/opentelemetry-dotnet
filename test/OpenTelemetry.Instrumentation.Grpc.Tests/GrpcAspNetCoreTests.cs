// <copyright file="GrpcAspNetCoreTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Greet;
using Grpc.Net.Client;
using Moq;
using OpenTelemetry.Instrumentation.Grpc.Tests.Services;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    [Collection("GrpcInstrumentation")]
    public class GrpcAspNetCoreTests : IClassFixture<GrpcFixture<GreeterService>>
    {
        private GrpcFixture<GreeterService> fixture;

        public GrpcAspNetCoreTests(GrpcFixture<GreeterService> fixture)
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
            Assert.Equal("grpc", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRPCSystem).Value);
            Assert.Equal("greet.Greeter", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRPCService).Value);
            Assert.Equal("SayHello", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRPCMethod).Value);
            Assert.Contains(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerIP).Value, clientLoopbackAddresses);
            Assert.True(!string.IsNullOrEmpty(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerPort).Value));
            Assert.Equal(Status.Ok, span.GetStatus());
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
