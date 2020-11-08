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
using System.Net.Http;
using System.Threading.Tasks;
using Greet;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public partial class GrpcTests
    {
        [Theory]
        [InlineData("http://localhost")]
        [InlineData("http://localhost", false)]
        [InlineData("http://127.0.0.1")]
        [InlineData("http://127.0.0.1", false)]
        [InlineData("http://[::1]")]
        [InlineData("http://[::1]", false)]
        public void GrpcClientCallsAreCollectedSuccessfully(string baseAddress, bool shouldEnrich = true)
        {
            var uri = new Uri($"{baseAddress}:{this.server.Port}");
            var uriHostNameType = Uri.CheckHostName(uri.Host);

            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation(options =>
                    {
                        if (shouldEnrich)
                        {
                            options.Enrich = ActivityEnrichment;
                        }
                    })
                    .AddProcessor(processor.Object)
                    .Build())
            {
                var channel = GrpcChannel.ForAddress(uri);
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
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GrpcAndHttpClientInstrumentationIsInvoked(bool shouldEnrich)
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent")
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation(options =>
                    {
                        if (shouldEnrich)
                        {
                            options.Enrich = ActivityEnrichment;
                        }
                    })
                    .AddHttpClientInstrumentation()
                    .AddProcessor(processor.Object)
                    .Build())
            {
                using var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(7, processor.Invocations.Count); // SetParentProvider + OnStart/OnEnd (gRPC) + OnStart/OnEnd (HTTP) + OnShutdown/Dispose called.
            var httpSpan = (Activity)processor.Invocations[3].Arguments[0];
            var grpcSpan = (Activity)processor.Invocations[4].Arguments[0];

            ValidateGrpcActivity(grpcSpan);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan.DisplayName);
            Assert.Equal($"HTTP POST", httpSpan.DisplayName);
            Assert.Equal(grpcSpan.SpanId, httpSpan.ParentSpanId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GrpcAndHttpClientInstrumentationWithSuppressInstrumentation(bool shouldEnrich)
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent")
                .Start();

            using (Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddGrpcClientInstrumentation(o =>
                    {
                        o.SuppressDownstreamInstrumentation = true;
                        if (shouldEnrich)
                        {
                            o.Enrich = ActivityEnrichment;
                        }
                    })
                    .AddHttpClientInstrumentation()
                    .AddProcessor(processor.Object)
                    .Build())
            {
                Parallel.ForEach(
                new int[4],
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                },
                (value) =>
                {
                    var channel = GrpcChannel.ForAddress(uri);
                    var client = new Greeter.GreeterClient(channel);
                    var rs = client.SayHello(new HelloRequest());
                });
            }

            Assert.Equal(11, processor.Invocations.Count); // SetParentProvider + OnStart/OnEnd (gRPC) * 4 + OnShutdown/Dispose called.
            var grpcSpan1 = (Activity)processor.Invocations[2].Arguments[0];
            var grpcSpan2 = (Activity)processor.Invocations[4].Arguments[0];
            var grpcSpan3 = (Activity)processor.Invocations[6].Arguments[0];
            var grpcSpan4 = (Activity)processor.Invocations[8].Arguments[0];

            ValidateGrpcActivity(grpcSpan1);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan1.DisplayName);

            ValidateGrpcActivity(grpcSpan2);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan2.DisplayName);

            ValidateGrpcActivity(grpcSpan3);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan3.DisplayName);

            ValidateGrpcActivity(grpcSpan4);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan4.DisplayName);
        }

        [Fact]
        public void GrpcPropagatesContextWithSuppressInstrumentation()
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var processor = new Mock<BaseProcessor<Activity>>();

            using var source = new ActivitySource("test-source");

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o =>
                {
                    o.SuppressDownstreamInstrumentation = true;
                })
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Enrich = (activity, eventName, obj) =>
                    {
                        switch (eventName)
                        {
                            case "OnStartActivity":
                                var request = (HttpRequest)obj;
                                activity.SetCustomProperty("BaggageString", request.Headers["Baggage"].ToString());
                                break;
                            default:
                                break;
                        }
                    };
                }) // Instrumenting the server side as well
                .AddProcessor(processor.Object)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                Assert.NotNull(activity);
                Baggage.Current.SetBaggage("item1", "value1");

                var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(8, processor.Invocations.Count); // OnStart/OnEnd * 3 (parent + gRPC client and server) + OnShutdown/Dispose called.
            var serverActivity = (Activity)processor.Invocations[3].Arguments[0];
            var clientActivity = (Activity)processor.Invocations[4].Arguments[0];

            Assert.Equal($"greet.Greeter/SayHello", clientActivity.DisplayName);
            Assert.Equal($"/greet.Greeter/SayHello", serverActivity.DisplayName);
            Assert.Equal(clientActivity.TraceId, serverActivity.TraceId);
            Assert.Equal(clientActivity.SpanId, serverActivity.ParentSpanId);
            Assert.Contains("item1=value1", serverActivity.GetCustomProperty("BaggageString") as string);
        }

        [Fact]
        public void Grpc_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddGrpcClientInstrumentation());
        }

        private static void ValidateGrpcActivity(Activity activityToValidate)
        {
            Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            switch (method)
            {
                case "OnStartActivity":
                    Assert.True(obj is HttpRequestMessage);
                    break;

                case "OnStopActivity":
                    Assert.True(obj is HttpResponseMessage);
                    break;

                default:
                    break;
            }
        }
    }
}
