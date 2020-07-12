﻿// <copyright file="GrpcAspNetCoreTests.cs" company="OpenTelemetry Authors">
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
using Greet;
using Grpc.Net.Client;
using OpenTelemetry.Instrumentation.Grpc.Tests.Services;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
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

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("grpc", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.RpcSystem).Value);
            Assert.Equal("greet.Greeter", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.RpcService).Value);
            Assert.Equal("SayHello", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.RpcMethod).Value);
            Assert.Contains(span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.NetPeerIp).Value, clientLoopbackAddresses);
            Assert.True(!string.IsNullOrEmpty(span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.NetPeerPort).Value));
            Assert.Equal("Ok", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
        }
    }
}
