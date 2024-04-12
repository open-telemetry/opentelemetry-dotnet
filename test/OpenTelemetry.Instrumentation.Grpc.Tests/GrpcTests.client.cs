// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Greet;
#if !NETFRAMEWORK
using Grpc.Core;
#endif
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
#if !NETFRAMEWORK
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Tests;
#endif
using OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Trace;
using Xunit;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

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
        bool enrichWithHttpRequestMessageCalled = false;
        bool enrichWithHttpResponseMessageCalled = false;

        var uri = new Uri($"{baseAddress}:1234");
        var uriHostNameType = Uri.CheckHostName(uri.Host);

        using var httpClient = ClientTestHelpers.CreateTestClient(async request =>
        {
            var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply());
            var response = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.OK);
            response.TrailingHeaders().Add("grpc-message", "value");
            return response;
        });

        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(options =>
                {
                    if (shouldEnrich)
                    {
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build())
        {
            var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
            {
                HttpClient = httpClient,
            });
            var client = new Greeter.GreeterClient(channel);
            var rs = client.SayHello(new HelloRequest());
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

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
            Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeServerSocketAddress));
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeServerSocketAddress));
            Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }

        Assert.Equal(uri.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        Assert.Equal(Status.Unset, activity.GetStatus());

        // Tags added by the library then removed from the instrumentation
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));

        if (shouldEnrich)
        {
            Assert.True(enrichWithHttpRequestMessageCalled);
            Assert.True(enrichWithHttpResponseMessageCalled);
        }
    }

#if NET6_0_OR_GREATER
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAndHttpClientInstrumentationIsInvoked(bool shouldEnrich)
    {
        var uri = new Uri($"http://localhost:{this.server.Port}");
        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(options =>
                {
                    if (shouldEnrich)
                    {
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpRequestMessage", "yes");
                        };

                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpResponseMessage", "yes");
                        };
                    }
                })
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
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

        Assert.Equal(2, exportedItems.Count);
        var httpSpan = exportedItems.Single(activity => activity.OperationName == OperationNameHttpOut);
        var grpcSpan = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

        ValidateGrpcActivity(grpcSpan);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan.DisplayName);
        Assert.Equal(0, grpcSpan.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        Assert.Equal("POST", httpSpan.DisplayName);
        Assert.Equal(grpcSpan.SpanId, httpSpan.ParentSpanId);

        if (shouldEnrich)
        {
            Assert.Single(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpRequestMessage" && tag.Value == "yes");
            Assert.Single(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpResponseMessage" && tag.Value == "yes");
        }
        else
        {
            Assert.Empty(grpcSpan.Tags.Where(tag => tag.Key == "enrichedWithHttpRequestMessage"));
            Assert.Empty(grpcSpan.Tags.Where(tag => tag.Key == "enrichedWithHttpResponseMessage"));
        }
    }

    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092")]
    public void GrpcAndHttpClientInstrumentationWithSuppressInstrumentation()
    {
        var uri = new Uri($"http://localhost:{this.server.Port}");
        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(o => o.SuppressDownstreamInstrumentation = true)
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
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

        Assert.Equal(4, exportedItems.Count);
        var grpcSpan1 = exportedItems[0];
        var grpcSpan2 = exportedItems[1];
        var grpcSpan3 = exportedItems[2];
        var grpcSpan4 = exportedItems[3];

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

    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092")]
    public void GrpcPropagatesContextWithSuppressInstrumentationOptionSetToTrue()
    {
        try
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var exportedItems = new List<Activity>();

            using var source = new ActivitySource("test-source");

            var propagator = new CustomTextMapPropagator();
            propagator.InjectValues.Add("customField", context => "customValue");

            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                propagator,
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
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetCustomProperty("customField", request.Headers["customField"].ToString());
                    };
                }) // Instrumenting the server side as well
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                Assert.NotNull(activity);
                var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest());
            }

            var serverActivity = exportedItems.Single(activity => activity.OperationName == OperationNameHttpRequestIn);
            var clientActivity = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

            Assert.Equal($"greet.Greeter/SayHello", clientActivity.DisplayName);
            Assert.Equal($"POST /greet.Greeter/SayHello", serverActivity.DisplayName);
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
            var exportedItems = new List<Activity>();
            using var source = new ActivitySource("test-source");

            bool isPropagatorCalled = false;
            var propagator = new CustomTextMapPropagator
            {
                Injected = (context) => isPropagatorCalled = true,
            };

            Sdk.SetDefaultTextMapPropagator(propagator);

            var headers = new Metadata();

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o =>
                {
                    o.SuppressDownstreamInstrumentation = false;
                })
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                var channel = GrpcChannel.ForAddress(uri);
                var client = new Greeter.GreeterClient(channel);
                var rs = client.SayHello(new HelloRequest(), headers);
            }

            Assert.Equal(2, exportedItems.Count);

            var parentActivity = exportedItems.Single(activity => activity.OperationName == "parent");
            var clientActivity = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

            Assert.Equal(clientActivity.ParentSpanId, parentActivity.SpanId);

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

    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092")]
    public void GrpcClientInstrumentationRespectsSdkSuppressInstrumentation()
    {
        try
        {
            var uri = new Uri($"http://localhost:{this.server.Port}");
            var exportedItems = new List<Activity>();

            using var source = new ActivitySource("test-source");

            bool isPropagatorCalled = false;
            var propagator = new CustomTextMapPropagator();
            propagator.Injected = (context) => isPropagatorCalled = true;

            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                propagator,
            }));

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o =>
                {
                    o.SuppressDownstreamInstrumentation = true;
                })
                .AddInMemoryExporter(exportedItems)
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
            Assert.Single(exportedItems);
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
                services.Configure<GrpcClientTraceInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<GrpcClientTraceInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
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
        Assert.Equal(GrpcClientDiagnosticListener.Version, activityToValidate.Source.Version);
        Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
    }
}
