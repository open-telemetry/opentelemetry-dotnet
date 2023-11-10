// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Diagnostics;
using System.Net;
using Greet;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Grpc.Services.Tests;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

public partial class GrpcTests : IDisposable
{
    private const string OperationNameHttpRequestIn = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    private const string OperationNameGrpcOut = "Grpc.Net.Client.GrpcOut";
    private const string OperationNameHttpOut = "System.Net.Http.HttpRequestOut";

    private readonly GrpcServer<GreeterService> server;

    public GrpcTests()
    {
        this.server = new GrpcServer<GreeterService>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes(bool? enableGrpcAspNetCoreSupport)
    {
        var exportedItems = new List<Activity>();
        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (enableGrpcAspNetCoreSupport.HasValue)
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation(options =>
            {
                options.EnableGrpcAspNetCoreSupport = enableGrpcAspNetCoreSupport.Value;
            });
        }
        else
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation();
        }

        using var tracerProvider = tracerProviderBuilder
            .AddInMemoryExporter(exportedItems)
            .Build();

        var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
        var uri = new Uri($"http://localhost:{this.server.Port}");

        using var channel = GrpcChannel.ForAddress(uri);
        var client = new Greeter.GreeterClient(channel);
        var returnMsg = client.SayHello(new HelloRequest()).Message;
        Assert.False(string.IsNullOrEmpty(returnMsg));

        WaitForExporterToReceiveItems(exportedItems, 1);
        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(ActivityKind.Server, activity.Kind);

        if (!enableGrpcAspNetCoreSupport.HasValue || enableGrpcAspNetCoreSupport.Value)
        {
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeNetPeerIp), clientLoopbackAddresses);
            Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }
        else
        {
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        Assert.Equal(Status.Unset, activity.GetStatus());

        // The following are http.* attributes that are also included on the span for the gRPC invocation.
        Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeNetHostName));
        Assert.Equal(this.server.Port, activity.GetTagValue(SemanticConventions.AttributeNetHostPort));
        Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
        Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpTarget));
        Assert.Equal($"http://localhost:{this.server.Port}/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
        Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);
    }

    // Tests for v1.21.0 Semantic Conventions for database client calls.
    // see the spec https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md
    // This test emits the new attributes.
    // This test method can replace the other (old) test method when this library is GA.
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes_New(bool? enableGrpcAspNetCoreSupport)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http" })
            .Build();

        var exportedItems = new List<Activity>();
        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration));

        if (enableGrpcAspNetCoreSupport.HasValue)
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation(options =>
            {
                options.EnableGrpcAspNetCoreSupport = enableGrpcAspNetCoreSupport.Value;
            });
        }
        else
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation();
        }

        using var tracerProvider = tracerProviderBuilder
            .AddInMemoryExporter(exportedItems)
            .Build();

        var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
        var uri = new Uri($"http://localhost:{this.server.Port}");

        using var channel = GrpcChannel.ForAddress(uri);
        var client = new Greeter.GreeterClient(channel);
        var returnMsg = client.SayHello(new HelloRequest()).Message;
        Assert.False(string.IsNullOrEmpty(returnMsg));

        WaitForExporterToReceiveItems(exportedItems, 1);
        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(ActivityKind.Server, activity.Kind);

        if (!enableGrpcAspNetCoreSupport.HasValue || enableGrpcAspNetCoreSupport.Value)
        {
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeClientAddress), clientLoopbackAddresses);
            Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeClientPort));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }
        else
        {
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        Assert.Equal(Status.Unset, activity.GetStatus());

        // The following are http.* attributes that are also included on the span for the gRPC invocation.
        Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(this.server.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal("http", activity.GetTagValue(SemanticConventions.AttributeUrlScheme));
        Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeUrlPath));
        Assert.Equal("2", activity.GetTagValue(SemanticConventions.AttributeNetworkProtocolVersion));
        Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeUserAgentOriginal) as string);
    }

    // Tests for v1.21.0 Semantic Conventions for database client calls.
    // see the spec https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md
    // This test emits both the new and older attributes.
    // This test method can be deleted when this library is GA.
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes_Dupe(bool? enableGrpcAspNetCoreSupport)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http/dup" })
            .Build();

        var exportedItems = new List<Activity>();
        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration));

        if (enableGrpcAspNetCoreSupport.HasValue)
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation(options =>
            {
                options.EnableGrpcAspNetCoreSupport = enableGrpcAspNetCoreSupport.Value;
            });
        }
        else
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation();
        }

        using var tracerProvider = tracerProviderBuilder
            .AddInMemoryExporter(exportedItems)
            .Build();

        var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
        var uri = new Uri($"http://localhost:{this.server.Port}");

        using var channel = GrpcChannel.ForAddress(uri);
        var client = new Greeter.GreeterClient(channel);
        var returnMsg = client.SayHello(new HelloRequest()).Message;
        Assert.False(string.IsNullOrEmpty(returnMsg));

        WaitForExporterToReceiveItems(exportedItems, 1);
        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(ActivityKind.Server, activity.Kind);

        // OLD
        if (!enableGrpcAspNetCoreSupport.HasValue || enableGrpcAspNetCoreSupport.Value)
        {
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeNetPeerIp), clientLoopbackAddresses);
            Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }
        else
        {
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        Assert.Equal(Status.Unset, activity.GetStatus());

        // The following are http.* attributes that are also included on the span for the gRPC invocation.
        Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeNetHostName));
        Assert.Equal(this.server.Port, activity.GetTagValue(SemanticConventions.AttributeNetHostPort));
        Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
        Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpTarget));
        Assert.Equal($"http://localhost:{this.server.Port}/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
        Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);

        // NEW
        if (!enableGrpcAspNetCoreSupport.HasValue || enableGrpcAspNetCoreSupport.Value)
        {
            Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
            Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
            Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeClientAddress), clientLoopbackAddresses);
            Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeClientPort));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }
        else
        {
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
            Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        }

        Assert.Equal(Status.Unset, activity.GetStatus());

        // The following are http.* attributes that are also included on the span for the gRPC invocation.
        Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(this.server.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal("http", activity.GetTagValue(SemanticConventions.AttributeUrlScheme));
        Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeUrlPath));
        Assert.Equal("2", activity.GetTagValue(SemanticConventions.AttributeNetworkProtocolVersion));
        Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeUserAgentOriginal) as string);
    }

#if NET6_0_OR_GREATER
    [Theory(Skip = "Skipping for .NET 6 and higher due to bug #3023")]
#endif
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributesWhenItCreatesNewActivity(bool? enableGrpcAspNetCoreSupport)
    {
        try
        {
            // B3Propagator along with the headers passed to the client.SayHello ensure that the instrumentation creates a sibling activity
            Sdk.SetDefaultTextMapPropagator(new Extensions.Propagators.B3Propagator());
            var exportedItems = new List<Activity>();
            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

            if (enableGrpcAspNetCoreSupport.HasValue)
            {
                tracerProviderBuilder.AddAspNetCoreInstrumentation(options =>
                {
                    options.EnableGrpcAspNetCoreSupport = enableGrpcAspNetCoreSupport.Value;
                });
            }
            else
            {
                tracerProviderBuilder.AddAspNetCoreInstrumentation();
            }

            using var tracerProvider = tracerProviderBuilder
                .AddInMemoryExporter(exportedItems)
                .Build();

            var clientLoopbackAddresses = new[] { IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString() };
            var uri = new Uri($"http://localhost:{this.server.Port}");

            using var channel = GrpcChannel.ForAddress(uri);
            var client = new Greeter.GreeterClient(channel);
            var headers = new Metadata
            {
                { "traceparent", "00-120dc44db5b736468afb112197b0dbd3-5dfbdf27ec544544-01" },
                { "x-b3-traceid", "120dc44db5b736468afb112197b0dbd3" },
                { "x-b3-spanid", "b0966f651b9e0126" },
                { "x-b3-sampled", "1" },
            };
            client.SayHello(new HelloRequest(), headers);

            WaitForExporterToReceiveItems(exportedItems, 1);
            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);

            if (!enableGrpcAspNetCoreSupport.HasValue || enableGrpcAspNetCoreSupport.Value)
            {
                Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystem));
                Assert.Equal("greet.Greeter", activity.GetTagValue(SemanticConventions.AttributeRpcService));
                Assert.Equal("SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
                Assert.Contains(activity.GetTagValue(SemanticConventions.AttributeNetPeerIp), clientLoopbackAddresses);
                Assert.NotEqual(0, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
                Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
                Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
                Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
            }
            else
            {
                Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
                Assert.NotNull(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
            }

            Assert.Equal(Status.Unset, activity.GetStatus());

            // The following are http.* attributes that are also included on the span for the gRPC invocation.
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeNetHostName));
            Assert.Equal(this.server.Port, activity.GetTagValue(SemanticConventions.AttributeNetHostPort));
            Assert.Equal("POST", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
            Assert.Equal("/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpTarget));
            Assert.Equal($"http://localhost:{this.server.Port}/greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
            Assert.StartsWith("grpc-dotnet", activity.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);
        }
        finally
        {
            // Set the SDK to use the default propagator for other unit tests
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
            new TraceContextPropagator(),
            new BaggagePropagator(),
            }));
        }
    }

    public void Dispose()
    {
        this.server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void WaitForExporterToReceiveItems(List<Activity> itemsReceived, int itemCount)
    {
        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                Thread.Sleep(10);
                return itemsReceived.Count >= itemCount;
            },
            TimeSpan.FromSeconds(1)));
    }

    private static void WaitForProcessorInvocations(Mock<BaseProcessor<Activity>> spanProcessor, int invocationCount)
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

    private static Activity GetActivityFromProcessorInvocation(Mock<BaseProcessor<Activity>> processor, string methodName, string activityOperationName)
    {
        return processor.Invocations
            .FirstOrDefault(invo =>
            {
                return invo.Method.Name == methodName
                    && (invo.Arguments[0] as Activity)?.OperationName == activityOperationName;
            })?.Arguments[0] as Activity;
    }
}
#endif
