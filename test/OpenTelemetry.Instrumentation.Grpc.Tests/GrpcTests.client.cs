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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Trace;
using Xunit;
using Status = OpenTelemetry.Trace.Status;

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
            var uri = new Uri($"{baseAddress}:1234");
            var uriHostNameType = Uri.CheckHostName(uri.Host);

            var httpClient = ClientTestHelpers.CreateTestClient(async request =>
            {
                var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply());
                var response = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.OK);
                response.TrailingHeaders().Add("grpc-message", "value");
                return response;
            });

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
                var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
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

#if NET6_0_OR_GREATER
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GrpcAndHttpClientInstrumentationIsInvoked(bool shouldEnrich)
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var processor = new Mock<BaseProcessor<Activity>>();
            processor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c => c.SetTag("enriched", "no"));
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
                // With net5, based on the grpc changes, the quantity of default activities changed.
                // TODO: This is a workaround. https://github.com/open-telemetry/opentelemetry-dotnet/issues/1490
                using var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions()
                {
                    HttpClient = new HttpClient(),
                });

                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            Assert.Equal(7, processor.Invocations.Count); // SetParentProvider + OnStart/OnEnd (gRPC) + OnStart/OnEnd (HTTP) + OnShutdown/Dispose called.
            var httpSpan = (Activity)processor.Invocations[3].Arguments[0];
            var grpcSpan = (Activity)processor.Invocations[4].Arguments[0];

            ValidateGrpcActivity(grpcSpan);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan.DisplayName);
            Assert.Equal(0, grpcSpan.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
            Assert.Equal($"HTTP POST", httpSpan.DisplayName);
            Assert.Equal(grpcSpan.SpanId, httpSpan.ParentSpanId);

            Assert.NotEmpty(grpcSpan.Tags.Where(tag => tag.Key == "enriched"));
            Assert.Equal(shouldEnrich ? "yes" : "no", grpcSpan.Tags.Where(tag => tag.Key == "enriched").FirstOrDefault().Value);
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
            Assert.Equal(0, grpcSpan1.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));

            ValidateGrpcActivity(grpcSpan2);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan2.DisplayName);
            Assert.Equal(0, grpcSpan2.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));

            ValidateGrpcActivity(grpcSpan3);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan3.DisplayName);
            Assert.Equal(0, grpcSpan3.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));

            ValidateGrpcActivity(grpcSpan4);
            Assert.Equal($"greet.Greeter/SayHello", grpcSpan4.DisplayName);
            Assert.Equal(0, grpcSpan4.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }

        [Fact]
        public void GrpcPropagatesContextWithSuppressInstrumentationOptionSetToTrue()
        {
            try
            {
                var uri = new Uri($"http://localhost:{this.server.Port}");
                var processor = new Mock<BaseProcessor<Activity>>();

                using var source = new ActivitySource("test-source");

                var propagator = new Mock<TextMapPropagator>();
                propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                    .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                    {
                        action(message, "customField", "customValue");
                    });

                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    propagator.Object,
                }));

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
                                    activity.SetCustomProperty("customField", request.Headers["customField"].ToString());
                                    break;
                                default:
                                    break;
                            }
                        };
                    }) // Instrumenting the server side as well
                    .AddProcessor(processor.Object)
                    .Build())
                {
                    using (var activity = source.StartActivity("parent"))
                    {
                        Assert.NotNull(activity);
                        var channel = GrpcChannel.ForAddress(uri);
                        var client = new Greeter.GreeterClient(channel);
                        var rs = client.SayHello(new HelloRequest());
                    }

                    WaitForProcessorInvocations(processor, 7);
                }

                Assert.Equal(9, processor.Invocations.Count); // SetParentProvider + (OnStart + OnEnd) * 3 (parent, gRPC client, and server) + Shutdown + Dispose called.

                Assert.Single(processor.Invocations, invo => invo.Method.Name == "SetParentProvider");
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), "parent"));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), OperationNameGrpcOut));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), OperationNameHttpRequestIn));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), OperationNameHttpRequestIn));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), OperationNameGrpcOut));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), "parent"));
                Assert.Single(processor.Invocations, invo => invo.Method.Name == "OnShutdown");
                Assert.Single(processor.Invocations, invo => invo.Method.Name == nameof(processor.Object.Dispose));

                var serverActivity = GetActivityFromProcessorInvocation(processor, nameof(processor.Object.OnEnd), OperationNameHttpRequestIn);
                var clientActivity = GetActivityFromProcessorInvocation(processor, nameof(processor.Object.OnEnd), OperationNameGrpcOut);

                Assert.Equal($"greet.Greeter/SayHello", clientActivity.DisplayName);
                Assert.Equal($"greet.Greeter/SayHello", serverActivity.DisplayName);
                Assert.Equal(clientActivity.TraceId, serverActivity.TraceId);
                Assert.Equal(clientActivity.SpanId, serverActivity.ParentSpanId);
                Assert.Equal(0, clientActivity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
                Assert.Equal("customValue", serverActivity.GetCustomProperty("customField") as string);
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public void GrpcDoesNotPropagateContextWithSuppressInstrumentationOptionSetToFalse()
        {
            try
            {
                var uri = new Uri($"http://localhost:{this.server.Port}");
                var processor = new Mock<BaseProcessor<Activity>>();

                using var source = new ActivitySource("test-source");

                bool isPropagatorCalled = false;
                var propagator = new Mock<TextMapPropagator>();
                propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                    .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                    {
                        isPropagatorCalled = true;
                    });

                Sdk.SetDefaultTextMapPropagator(propagator.Object);

                var headers = new Metadata();

                using (Sdk.CreateTracerProviderBuilder()
                    .AddSource("test-source")
                    .AddGrpcClientInstrumentation(o =>
                    {
                        o.SuppressDownstreamInstrumentation = false;
                    })
                    .AddProcessor(processor.Object)
                    .Build())
                {
                    using var activity = source.StartActivity("parent");
                    var channel = GrpcChannel.ForAddress(uri);
                    var client = new Greeter.GreeterClient(channel);
                    var rs = client.SayHello(new HelloRequest(), headers);
                }

                Assert.Equal(7, processor.Invocations.Count); // SetParentProvider/OnShutdown/Dispose called.

                Assert.Single(processor.Invocations, invo => invo.Method.Name == "SetParentProvider");
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), "parent"));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), OperationNameGrpcOut));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), OperationNameGrpcOut));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), "parent"));
                Assert.Single(processor.Invocations, invo => invo.Method.Name == "OnShutdown");
                Assert.Single(processor.Invocations, invo => invo.Method.Name == nameof(processor.Object.Dispose));

                // Propagator is not called
                Assert.False(isPropagatorCalled);
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public void GrpcClientInstrumentationRespectsSdkSuppressInstrumentation()
        {
            try
            {
                var uri = new Uri($"http://localhost:{this.server.Port}");
                var processor = new Mock<BaseProcessor<Activity>>();

                using var source = new ActivitySource("test-source");

                bool isPropagatorCalled = false;
                var propagator = new Mock<TextMapPropagator>();
                propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                    .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                    {
                        isPropagatorCalled = true;
                    });

                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    propagator.Object,
                }));

                using (Sdk.CreateTracerProviderBuilder()
                    .AddSource("test-source")
                    .AddGrpcClientInstrumentation(o =>
                    {
                        o.SuppressDownstreamInstrumentation = true;
                    })
                    .AddProcessor(processor.Object)
                    .Build())
                {
                    using var activity = source.StartActivity("parent");
                    using (SuppressInstrumentationScope.Begin())
                    {
                        var channel = GrpcChannel.ForAddress(uri);
                        var client = new Greeter.GreeterClient(channel);
                        var rs = client.SayHello(new HelloRequest());
                    }
                }

                // If suppressed, activity is not emitted and
                // propagation is also not performed.
                Assert.Equal(5, processor.Invocations.Count); // SetParentProvider + (OnStart + OnEnd) * 3 for parent + OnShutdown + Dispose called.
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnStart), "parent"));
                Assert.Single(processor.Invocations, GeneratePredicateForMoqProcessorActivity(nameof(processor.Object.OnEnd), "parent"));
                Assert.False(isPropagatorCalled);
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }
#endif

        [Fact]
        public void AddGrpcClientInstrumentationNamedOptionsSupported()
        {
            int defaultExporterOptionsConfigureOptionsInvocations = 0;
            int namedExporterOptionsConfigureOptionsInvocations = 0;

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<GrpcClientInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                    services.Configure<GrpcClientInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
                })
                .AddGrpcClientInstrumentation()
                .AddGrpcClientInstrumentation("Instrumentation2", configure: null)
                .Build();

            Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
            Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
        }

        [Fact]
        public void Grpc_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddGrpcClientInstrumentation());
        }

        private static void ValidateGrpcActivity(Activity activityToValidate)
        {
            Assert.Equal(GrpcClientDiagnosticListener.ActivitySourceName, activityToValidate.Source.Name);
            Assert.Equal(GrpcClientDiagnosticListener.Version.ToString(), activityToValidate.Source.Version);
            Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            Assert.True(activity.IsAllDataRequested);
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

            activity.SetTag("enriched", "yes");
        }

        private static Predicate<IInvocation> GeneratePredicateForMoqProcessorActivity(string methodName, string activityOperationName)
        {
            return invo => invo.Method.Name == methodName && (invo.Arguments[0] as Activity)?.OperationName == activityOperationName;
        }
    }
}
