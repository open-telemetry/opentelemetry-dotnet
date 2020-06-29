// <copyright file="GrpcClientTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using OpenTelemetry.Instrumentation.GrpcClient.Internal.Tests;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.GrpcClient.Tests
{
    public class GrpcClientTests
    {
        [Theory]
        [InlineData("https://localhost")]
        [InlineData("https://127.0.0.1")]
        [InlineData("https://[::1]")]
        public async Task GrpcClientCallsAreCollectedSuccessfully(string baseAddress)
        {
            var uri = new Uri(baseAddress);
            var uriHostNameType = Uri.CheckHostName(uri.Host);

            var spanProcessor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            using (OpenTelemetrySdk.EnableOpenTelemetry(
                (builder) => builder
                    .AddGrpcClientDependencyInstrumentation()
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
            {
                var httpClient = ClientTestHelpers.CreateTestClient(
                    async httpRequestMessage =>
                    {
                        HelloReply reply = new HelloReply
                        {
                            Message = "Hello world",
                        };

                        var streamContent = await ClientTestHelpers.CreateResponseContent(reply).TimeoutAfter(TimeSpan.FromSeconds(5));

                        return ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent);
                    },
                    uri);

                var channelOptions = new GrpcChannelOptions
                {
                    HttpClient = httpClient,
                };

                var channel = GrpcChannel.ForAddress(httpClient.BaseAddress, channelOptions);
                var invoker = channel.CreateCallInvoker();
                var rs = await invoker.AsyncUnaryCall<HelloRequest, HelloReply>(ClientTestHelpers.ServiceMethod, string.Empty, default(CallOptions), new HelloRequest());
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            Assert.Equal($"{ClientTestHelpers.ServiceMethod.ServiceName}/{ClientTestHelpers.ServiceMethod.Name}", span.DisplayName);
            Assert.Equal("Client", span.Kind.ToString());
            Assert.Equal("grpc", span.Tags.FirstOrDefault(i => i.Key == "rpc.system").Value);
            Assert.Equal(ClientTestHelpers.ServiceMethod.ServiceName, span.Tags.FirstOrDefault(i => i.Key == "rpc.service").Value);
            Assert.Equal(ClientTestHelpers.ServiceMethod.Name, span.Tags.FirstOrDefault(i => i.Key == "rpc.method").Value);

            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                Assert.Equal(uri.Host, span.Tags.FirstOrDefault(i => i.Key == "net.peer.ip").Value);
                Assert.Null(span.Tags.FirstOrDefault(i => i.Key == "net.peer.name").Value);
            }
            else
            {
                Assert.Null(span.Tags.FirstOrDefault(i => i.Key == "net.peer.ip").Value);
                Assert.Equal(uri.Host, span.Tags.FirstOrDefault(i => i.Key == "net.peer.name").Value);
            }

            Assert.Equal("443", span.Tags.FirstOrDefault(i => i.Key == "net.peer.port").Value);
            Assert.Equal("Ok", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
        }
    }
}
