// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class MockCollectorIntegrationTests
{
    private static int gRPCPort = 4317;
    private static int httpPort = 5051;

    [Fact]
    public async Task TestRecoveryAfterFailedExport()
    {
        using var host = await new HostBuilder()
           .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(5050, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
                    options.ListenLocalhost(4317, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
                })
               .ConfigureServices(services =>
               {
                   services.AddSingleton(new MockCollectorState());
                   services.AddGrpc();
               })
               .Configure(app =>
               {
                   app.UseRouting();

                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapGet(
                           "/MockCollector/SetResponseCodes/{responseCodesCsv}",
                           (MockCollectorState collectorState, string responseCodesCsv) =>
                           {
                               var codes = responseCodesCsv.Split(",").Select(x => int.Parse(x)).ToArray();
                               collectorState.SetStatusCodes(codes);
                           });

                       endpoints.MapGrpcService<MockTraceService>();
                   });
               }))
           .StartAsync();

        var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };

        var codes = new[] { Grpc.Core.StatusCode.Unimplemented, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var exportResults = new List<ExportResult>();
        var otlpExporter = new OtlpTraceExporter(new OtlpExporterOptions() { Endpoint = new Uri("http://localhost:4317") });
        var delegatingExporter = new DelegatingExporter<Activity>
        {
            OnExportFunc = (batch) =>
            {
                var result = otlpExporter.Export(batch);
                exportResults.Add(result);
                return result;
            },
        };

        var activitySourceName = "otel.mock.collector.test";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddProcessor(new SimpleActivityExportProcessor(delegatingExporter))
            .Build();

        using var source = new ActivitySource(activitySourceName);

        source.StartActivity().Stop();

        Assert.Single(exportResults);
        Assert.Equal(ExportResult.Failure, exportResults[0]);

        source.StartActivity().Stop();

        Assert.Equal(2, exportResults.Count);
        Assert.Equal(ExportResult.Success, exportResults[1]);

        await host.StopAsync();
    }

    [Theory]
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.Unavailable)]
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.Cancelled)]
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.Aborted)]
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.OutOfRange)]
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.DataLoss)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.Internal)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.InvalidArgument)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.Unimplemented)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.FailedPrecondition)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.PermissionDenied)]
    [InlineData(true, ExportResult.Failure, Grpc.Core.StatusCode.Unauthenticated)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Unavailable)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Cancelled)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Aborted)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.OutOfRange)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.DataLoss)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Internal)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.InvalidArgument)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.FailedPrecondition)]
    public async Task GrpcRetryTests(bool useRetryTransmissionHandler, ExportResult expectedResult, Grpc.Core.StatusCode initialStatusCode)
    {
        var testGrpcPort = Interlocked.Increment(ref gRPCPort);
        var testHttpPort = Interlocked.Increment(ref httpPort);

        using var host = await new HostBuilder()
           .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(testHttpPort, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
                    options.ListenLocalhost(testGrpcPort, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
                })
               .ConfigureServices(services =>
               {
                   services.AddSingleton(new MockCollectorState());
                   services.AddGrpc();
               })
               .Configure(app =>
               {
                   app.UseRouting();

                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapGet(
                           "/MockCollector/SetResponseCodes/{responseCodesCsv}",
                           (MockCollectorState collectorState, string responseCodesCsv) =>
                           {
                               var codes = responseCodesCsv.Split(",").Select(x => int.Parse(x)).ToArray();
                               collectorState.SetStatusCodes(codes);
                           });

                       endpoints.MapGrpcService<MockTraceService>();
                   });
               }))
           .StartAsync();

        var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        // First reply with failure and then Ok
        var codes = new[] { initialStatusCode, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testGrpcPort}");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000 };

        var exportClient = new OtlpGrpcTraceExportClient(exporterOptions);

        OtlpExporterTransmissionHandler<ExportTraceServiceRequest> transmissionHandler;

        // TODO: update this to configure via experimental environment variable.
        if (useRetryTransmissionHandler)
        {
            transmissionHandler = new OtlpExporterRetryTransmissionHandler<ExportTraceServiceRequest>(exportClient, exporterOptions.TimeoutMilliseconds);
        }
        else
        {
            transmissionHandler = new OtlpExporterTransmissionHandler<ExportTraceServiceRequest>(exportClient, exporterOptions.TimeoutMilliseconds);
        }

        var otlpExporter = new OtlpTraceExporter(exporterOptions, new(), transmissionHandler);

        var activitySourceName = "otel.grpc.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        var activity = source.StartActivity("GrpcRetryTest");
        activity.Stop();
        var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);

        await host.StopAsync();
    }

    [Theory]
    [InlineData(true, ExportResult.Success, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.BadGateway)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.GatewayTimeout)]
    [InlineData(true, ExportResult.Failure, HttpStatusCode.BadRequest)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.BadGateway)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.GatewayTimeout)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.TooManyRequests)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.TooManyRequests)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.BadRequest)]
    public async Task HttpRetryTests(bool useRetryTransmissionHandler, ExportResult expectedResult, HttpStatusCode initialHttpStatusCode)
    {
        var testHttpPort = Interlocked.Increment(ref httpPort);

        using var host = await new HostBuilder()
           .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(testHttpPort, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
                })
               .ConfigureServices(services =>
               {
                   services.AddSingleton(new MockCollectorHttpState());
               })
               .Configure(app =>
               {
                   app.UseRouting();

                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapGet(
                           "/MockCollector/SetResponseCodes/{responseCodesCsv}",
                           (MockCollectorHttpState collectorState, string responseCodesCsv) =>
                           {
                               var codes = responseCodesCsv.Split(",").Select(x => int.Parse(x)).ToArray();
                               collectorState.SetStatusCodes(codes);
                           });

                       endpoints.MapPost("/v1/traces", async ctx =>
                       {
                           var state = ctx.RequestServices.GetRequiredService<MockCollectorHttpState>();
                           ctx.Response.StatusCode = (int)state.NextStatus();

                           await ctx.Response.WriteAsync("Request Received.");
                       });
                   });
               }))
           .StartAsync();

        var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        var codes = new[] { initialHttpStatusCode, HttpStatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testHttpPort}/v1/traces");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000 };

        var exportClient = new OtlpHttpTraceExportClient(exporterOptions, new HttpClient());

        OtlpExporterTransmissionHandler<ExportTraceServiceRequest> transmissionHandler;

        // TODO: update this to configure via experimental environment variable.
        if (useRetryTransmissionHandler)
        {
            transmissionHandler = new OtlpExporterRetryTransmissionHandler<ExportTraceServiceRequest>(exportClient, exporterOptions.TimeoutMilliseconds);
        }
        else
        {
            transmissionHandler = new OtlpExporterTransmissionHandler<ExportTraceServiceRequest>(exportClient, exporterOptions.TimeoutMilliseconds);
        }

        var otlpExporter = new OtlpTraceExporter(exporterOptions, new(), transmissionHandler);

        var activitySourceName = "otel.http.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        var activity = source.StartActivity("HttpRetryTest");
        activity.Stop();
        var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);
    }

    private class MockCollectorState
    {
        private Grpc.Core.StatusCode[] statusCodes = { };
        private int statusCodeIndex = 0;

        public void SetStatusCodes(int[] statusCodes)
        {
            this.statusCodeIndex = 0;
            this.statusCodes = statusCodes.Select(x => (Grpc.Core.StatusCode)x).ToArray();
        }

        public Grpc.Core.StatusCode NextStatus()
        {
            return this.statusCodeIndex < this.statusCodes.Length
                ? this.statusCodes[this.statusCodeIndex++]
                : Grpc.Core.StatusCode.OK;
        }
    }

    private class MockCollectorHttpState
    {
        private HttpStatusCode[] statusCodes = { };
        private int statusCodeIndex = 0;

        public void SetStatusCodes(int[] statusCodes)
        {
            this.statusCodeIndex = 0;
            this.statusCodes = statusCodes.Select(x => (HttpStatusCode)x).ToArray();
        }

        public HttpStatusCode NextStatus()
        {
            return this.statusCodeIndex < this.statusCodes.Length
                ? this.statusCodes[this.statusCodeIndex++]
                : HttpStatusCode.OK;
        }
    }

    private class MockTraceService : TraceService.TraceServiceBase
    {
        private readonly MockCollectorState state;

        public MockTraceService(MockCollectorState state)
        {
            this.state = state;
        }

        public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
        {
            var statusCode = this.state.NextStatus();
            if (statusCode != Grpc.Core.StatusCode.OK)
            {
                throw new RpcException(new Grpc.Core.Status(statusCode, "Error."));
            }

            return Task.FromResult(new ExportTraceServiceResponse());
        }
    }
}
#endif
