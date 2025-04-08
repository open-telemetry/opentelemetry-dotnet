// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.PersistentStorage.Abstractions;
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
               .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders())
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

        using var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        var codes = new[] { Grpc.Core.StatusCode.Unimplemented, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var exportResults = new List<ExportResult>();
        using var otlpExporter = new OtlpTraceExporter(new OtlpExporterOptions() { Endpoint = new Uri($"http://localhost:{testGrpcPort}") });
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

        source.StartActivity()?.Stop();

        Assert.Single(exportResults);
        Assert.Equal(ExportResult.Failure, exportResults[0]);

        source.StartActivity()?.Stop();

        Assert.Equal(2, exportResults.Count);
        Assert.Equal(ExportResult.Success, exportResults[1]);

        await host.StopAsync();
    }

    // For `Grpc.Core.StatusCode.DeadlineExceeded`
    // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/5436.
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
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.DeadlineExceeded)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Unavailable)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Cancelled)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Aborted)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.OutOfRange)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.DataLoss)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Internal)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.InvalidArgument)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.FailedPrecondition)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.DeadlineExceeded)]
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
               .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders())
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

        using var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        // First reply with failure and then Ok
        var codes = new[] { initialStatusCode, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testGrpcPort}");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000, Protocol = OtlpExportProtocol.Grpc };

        var configuration = new ConfigurationBuilder()
                                 .AddInMemoryCollection(new Dictionary<string, string?>
                                 {
                                     [ExperimentalOptions.OtlpRetryEnvVar] = useRetryTransmissionHandler ? "in_memory" : null,
                                 })
                                 .Build();

        using var otlpExporter = new OtlpTraceExporter(exporterOptions, new SdkLimitOptions(), new ExperimentalOptions(configuration));

        var activitySourceName = "otel.grpc.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var activity = source.StartActivity("GrpcRetryTest");
        Assert.NotNull(activity);
        activity.Stop();
        using var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);

        await host.StopAsync();
    }

    [Theory]
    [InlineData(true, ExportResult.Success, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.BadGateway)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.GatewayTimeout)]
    [InlineData(true, ExportResult.Failure, HttpStatusCode.BadRequest)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.TooManyRequests)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.BadGateway)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.GatewayTimeout)]
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
               .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders())
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

        using var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        var codes = new[] { initialHttpStatusCode, HttpStatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testHttpPort}/v1/traces");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000, Protocol = OtlpExportProtocol.HttpProtobuf };

        var configuration = new ConfigurationBuilder()
                                 .AddInMemoryCollection(new Dictionary<string, string?>
                                 {
                                     [ExperimentalOptions.OtlpRetryEnvVar] = useRetryTransmissionHandler ? "in_memory" : null,
                                 })
                                 .Build();

        using var otlpExporter = new OtlpTraceExporter(exporterOptions, new SdkLimitOptions(), new ExperimentalOptions(configuration));

        var activitySourceName = "otel.http.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var activity = source.StartActivity("HttpRetryTest");
        Assert.NotNull(activity);
        activity.Stop();
        using var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);
    }

    [Theory]
    [InlineData(true, ExportResult.Success, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.BadGateway)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.GatewayTimeout)]
    [InlineData(true, ExportResult.Failure, HttpStatusCode.BadRequest)]
    [InlineData(true, ExportResult.Success, HttpStatusCode.TooManyRequests)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.BadGateway)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.GatewayTimeout)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.TooManyRequests)]
    [InlineData(false, ExportResult.Failure, HttpStatusCode.BadRequest)]
    public async Task HttpPersistentStorageRetryTests(bool usePersistentStorageTransmissionHandler, ExportResult expectedResult, HttpStatusCode initialHttpStatusCode)
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
               .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders())
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

        using var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        var codes = new[] { initialHttpStatusCode, HttpStatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testHttpPort}/v1/traces");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000 };

        var exportClient = new OtlpHttpExportClient(exporterOptions, new HttpClient(), "/v1/traces");

        // TODO: update this to configure via experimental environment variable.
        OtlpExporterTransmissionHandler transmissionHandler;
        MockFileProvider? mockProvider = null;
        if (usePersistentStorageTransmissionHandler)
        {
            mockProvider = new MockFileProvider();
            transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(
                mockProvider,
                exportClient,
                exporterOptions.TimeoutMilliseconds);
        }
        else
        {
            transmissionHandler = new OtlpExporterTransmissionHandler(exportClient, exporterOptions.TimeoutMilliseconds);
        }

        using var otlpExporter = new OtlpTraceExporter(exporterOptions, new(), new(), transmissionHandler);

        var activitySourceName = "otel.http.persistent.storage.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var activity = source.StartActivity("HttpPersistentStorageRetryTest");
        Assert.NotNull(activity);
        activity.Stop();
        using var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);

        if (usePersistentStorageTransmissionHandler)
        {
            Assert.NotNull(mockProvider);
            if (exportResult == ExportResult.Success)
            {
                Assert.Single(mockProvider.TryGetBlobs());

                // Force Retry
                Assert.True((transmissionHandler as OtlpExporterPersistentStorageTransmissionHandler)?.InitiateAndWaitForRetryProcess(-1));

                Assert.False(mockProvider.TryGetBlob(out _));
            }
            else
            {
                Assert.Empty(mockProvider.TryGetBlobs());
            }
        }
        else
        {
            Assert.Null(mockProvider);
        }

        transmissionHandler.Shutdown(0);

        transmissionHandler.Dispose();
    }

    // For `Grpc.Core.StatusCode.DeadlineExceeded`
    // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/5436.
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
    [InlineData(true, ExportResult.Success, Grpc.Core.StatusCode.DeadlineExceeded)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Unavailable)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Cancelled)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Aborted)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.OutOfRange)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.DataLoss)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.Internal)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.InvalidArgument)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.FailedPrecondition)]
    [InlineData(false, ExportResult.Failure, Grpc.Core.StatusCode.DeadlineExceeded)]
    public async Task GrpcPersistentStorageRetryTests(bool usePersistentStorageTransmissionHandler, ExportResult expectedResult, Grpc.Core.StatusCode initialgrpcStatusCode)
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
               .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders())
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

        using var httpClient = new HttpClient() { BaseAddress = new Uri($"http://localhost:{testHttpPort}") };

        var codes = new[] { initialgrpcStatusCode, Grpc.Core.StatusCode.OK };
        await httpClient.GetAsync($"/MockCollector/SetResponseCodes/{string.Join(",", codes.Select(x => (int)x))}");

        var endpoint = new Uri($"http://localhost:{testGrpcPort}");

        var exporterOptions = new OtlpExporterOptions() { Endpoint = endpoint, TimeoutMilliseconds = 20000 };

        var exportClient = new OtlpGrpcExportClient(exporterOptions, new HttpClient(), "opentelemetry.proto.collector.trace.v1.TraceService/Export");

        // TODO: update this to configure via experimental environment variable.
        OtlpExporterTransmissionHandler transmissionHandler;
        MockFileProvider? mockProvider = null;
        if (usePersistentStorageTransmissionHandler)
        {
            mockProvider = new MockFileProvider();
            transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(
                mockProvider,
                exportClient,
                exporterOptions.TimeoutMilliseconds);
        }
        else
        {
            transmissionHandler = new OtlpExporterTransmissionHandler(exportClient, exporterOptions.TimeoutMilliseconds);
        }

        using var otlpExporter = new OtlpTraceExporter(exporterOptions, new(), new(), transmissionHandler);

        var activitySourceName = "otel.grpc.persistent.storage.retry.test";
        using var source = new ActivitySource(activitySourceName);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var activity = source.StartActivity("GrpcPersistentStorageRetryTest");
        Assert.NotNull(activity);
        activity.Stop();
        using var batch = new Batch<Activity>([activity], 1);

        var exportResult = otlpExporter.Export(batch);

        Assert.Equal(expectedResult, exportResult);

        if (usePersistentStorageTransmissionHandler)
        {
            Assert.NotNull(mockProvider);
            if (exportResult == ExportResult.Success)
            {
                Assert.Single(mockProvider.TryGetBlobs());

                // Force Retry
                Assert.True((transmissionHandler as OtlpExporterPersistentStorageTransmissionHandler)?.InitiateAndWaitForRetryProcess(-1));

                Assert.False(mockProvider.TryGetBlob(out _));
            }
            else
            {
                Assert.Empty(mockProvider.TryGetBlobs());
            }
        }
        else
        {
            Assert.Null(mockProvider);
        }

        transmissionHandler.Shutdown(0);

        transmissionHandler.Dispose();
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

    private class MockFileProvider : PersistentBlobProvider
    {
        private readonly List<PersistentBlob> mockStorage = new();

        public IEnumerable<PersistentBlob> TryGetBlobs() => this.mockStorage.AsEnumerable();

        protected override IEnumerable<PersistentBlob> OnGetBlobs()
        {
            return this.mockStorage.AsEnumerable();
        }

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob)
        {
            blob = new MockFileBlob(this.mockStorage);
            return blob.TryWrite(buffer);
        }

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob)
        {
            blob = new MockFileBlob(this.mockStorage);
            return blob.TryWrite(buffer);
        }

        protected override bool OnTryGetBlob([NotNullWhen(true)] out PersistentBlob? blob)
        {
            blob = this.GetBlobs().FirstOrDefault();

            return blob != null;
        }
    }

    private class MockFileBlob : PersistentBlob
    {
        private readonly List<PersistentBlob> mockStorage;

        private byte[] buffer = Array.Empty<byte>();

        public MockFileBlob(List<PersistentBlob> mockStorage)
        {
            this.mockStorage = mockStorage;
        }

        protected override bool OnTryRead(out byte[] buffer)
        {
            buffer = this.buffer;

            return true;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            this.buffer = buffer;
            this.mockStorage.Add(this);

            return true;
        }

        protected override bool OnTryLease(int leasePeriodMilliseconds)
        {
            return true;
        }

        protected override bool OnTryDelete()
        {
            return this.mockStorage.Remove(this);
        }
    }
}
#endif
