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
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Grpc.Services.Tests;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using Xunit;
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
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributes(string enableGrpcAspNetCoreSupport)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION"] = enableGrpcAspNetCoreSupport,
            })
            .Build();

        var exportedItems = new List<Activity>();
        using var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
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

        if (enableGrpcAspNetCoreSupport != null && enableGrpcAspNetCoreSupport.Equals("true", StringComparison.OrdinalIgnoreCase))
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
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    public void GrpcAspNetCoreInstrumentationAddsCorrectAttributesWhenItCreatesNewActivity(string enableGrpcAspNetCoreSupport)
    {
        try
        {
            // B3Propagator along with the headers passed to the client.SayHello ensure that the instrumentation creates a sibling activity
            Sdk.SetDefaultTextMapPropagator(new Extensions.Propagators.B3Propagator());
            var exportedItems = new List<Activity>();
            var configuration = new ConfigurationBuilder()
           .AddInMemoryCollection(new Dictionary<string, string>
           {
               ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION"] = enableGrpcAspNetCoreSupport,
           })
           .Build();

            using var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
                .AddAspNetCoreInstrumentation()
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

            if (enableGrpcAspNetCoreSupport != null && enableGrpcAspNetCoreSupport.Equals("true", StringComparison.OrdinalIgnoreCase))
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
}
#endif
