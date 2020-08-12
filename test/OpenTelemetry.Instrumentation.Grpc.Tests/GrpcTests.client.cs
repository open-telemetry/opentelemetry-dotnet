// <copyright file="GrpcTests.client.cs" company="OpenTelemetry Authors">
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
        [Theory]
        [InlineData("http://localhost")]
        [InlineData("http://127.0.0.1")]
        [InlineData("http://[::1]")]
        public void GrpcClientCallsAreCollectedSuccessfully(string baseAddress)
        {
            var uri = new Uri($"{baseAddress}:{this.fixture.Port}");
            var uriHostNameType = Uri.CheckHostName(uri.Host);

            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var spanProcessor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent")
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation()
                    .SetResource(expectedResource)
                    .AddProcessor(spanProcessor.Object)
                    .Build())
            {
                var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(3, spanProcessor.Invocations.Count); // start/end/dispose was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            Assert.Equal($"greet.Greeter/SayHello", span.DisplayName);
            Assert.Equal("Client", span.Kind.ToString());
            Assert.Equal("grpc", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcSystem).Value);
            Assert.Equal("greet.Greeter", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcService).Value);
            Assert.Equal("SayHello", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeRpcMethod).Value);

            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                Assert.Equal(uri.Host, span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerIp).Value);
                Assert.Null(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerName).Value);
            }
            else
            {
                Assert.Null(span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerIp).Value);
                Assert.Equal(uri.Host, span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerName).Value);
            }

            Assert.Equal(uri.Port.ToString(), span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeNetPeerPort).Value);
            Assert.Equal(Status.Ok, span.GetStatus());
            Assert.Equal(expectedResource, span.GetResource());
        }

        [Fact]
        public void GrpcAndHttpClientInstrumentationIsInvoked()
        {
            var uri = new Uri($"http://localhost:{this.fixture.Port}");

            var spanProcessor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent")
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessor(spanProcessor.Object)
                    .Build())
            {
                var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(5, spanProcessor.Invocations.Count); // begin and end was called for Grpc call and underlying Http call + dispose
            var httpSpan = (Activity)spanProcessor.Invocations[2].Arguments[0];
            var grpcSpan = (Activity)spanProcessor.Invocations[3].Arguments[0];

            Assert.Equal($"greet.Greeter/SayHello", grpcSpan.DisplayName);
            Assert.Equal($"HTTP POST", httpSpan.DisplayName);
            Assert.Equal(grpcSpan.SpanId, httpSpan.ParentSpanId);
        }

        [Fact]
        public void Grpc_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddGrpcClientInstrumentation());
        }
    }
}
