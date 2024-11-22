// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OtlpCollector = OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Trace.V1;

namespace Benchmarks.Exporter;

[MemoryDiagnoser]
public class OtlpTraceExporterBenchmarks
{
    private OtlpTraceExporter? exporter;
    private ProtobufOtlpTraceExporter? protobufOtlpTraceExporter;
    private Activity? activity;
    private CircularBuffer<Activity>? activityBatch;

    private IHost? host;
    private IDisposable? server;
    private string? serverHost;
    private int serverPort;

    [GlobalSetup(Target = nameof(OtlpTraceExporter_Grpc))]
    public void GlobalSetupGrpc()
    {
        this.host = new HostBuilder()
          .ConfigureWebHostDefaults(webBuilder => webBuilder
               .ConfigureKestrel(options =>
               {
                   options.ListenLocalhost(4317, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
               })
              .ConfigureServices(services =>
              {
                  services.AddGrpc();
              })
              .Configure(app =>
              {
                  app.UseRouting();
                  app.UseEndpoints(endpoints =>
                  {
                      endpoints.MapGrpcService<MockTraceService>();
                  });
              }))
          .Start();

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpTraceExporter(options);

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(1);
        this.activityBatch.Add(this.activity);
    }

    [GlobalSetup(Target = nameof(OtlpTraceExporter_Http))]
    public void GlobalSetupHttp()
    {
        this.server = TestHttpServer.RunServer(
            (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            },
            out this.serverHost,
            out this.serverPort);

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };
        this.exporter = new OtlpTraceExporter(options);

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(1);
        this.activityBatch.Add(this.activity);
    }

    [GlobalSetup(Target = nameof(ProtobufOtlpTraceExporter_Grpc))]
    public void GlobalSetupProtobufGrpc()
    {
        this.host = new HostBuilder()
          .ConfigureWebHostDefaults(webBuilder => webBuilder
               .ConfigureKestrel(options =>
               {
                   options.ListenLocalhost(4317, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
               })
              .ConfigureServices(services =>
              {
                  services.AddGrpc();
              })
              .Configure(app =>
              {
                  app.UseRouting();
                  app.UseEndpoints(endpoints =>
                  {
                      endpoints.MapGrpcService<MockTraceService>();
                  });
              }))
          .Start();

        var options = new OtlpExporterOptions();
        this.protobufOtlpTraceExporter = new ProtobufOtlpTraceExporter(options);

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(1);
        this.activityBatch.Add(this.activity);
    }

    [GlobalSetup(Target = nameof(ProtobufOtlpTraceExporter_Http))]
    public void GlobalSetupProtobufHttp()
    {
        this.server = TestHttpServer.RunServer(
            (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            },
            out this.serverHost,
            out this.serverPort);

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };
        this.protobufOtlpTraceExporter = new ProtobufOtlpTraceExporter(options);

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(1);
        this.activityBatch.Add(this.activity);
    }

    [GlobalCleanup(Target = nameof(OtlpTraceExporter_Grpc))]
    public void GlobalCleanupGrpc()
    {
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
        this.activity?.Dispose();
        this.host?.Dispose();
    }

    [GlobalCleanup(Target = nameof(OtlpTraceExporter_Http))]
    public void GlobalCleanupHttp()
    {
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
        this.server?.Dispose();
        this.activity?.Dispose();
    }

    [GlobalCleanup(Target = nameof(ProtobufOtlpTraceExporter_Grpc))]
    public void GlobalCleanupProtobufGrpc()
    {
        this.protobufOtlpTraceExporter?.Shutdown();
        this.protobufOtlpTraceExporter?.Dispose();
        this.activity?.Dispose();
        this.host?.Dispose();
    }

    [GlobalCleanup(Target = nameof(ProtobufOtlpTraceExporter_Http))]
    public void GlobalCleanupProtobufHttp()
    {
        this.protobufOtlpTraceExporter?.Shutdown();
        this.protobufOtlpTraceExporter?.Dispose();
        this.server?.Dispose();
        this.activity?.Dispose();
    }

    [Benchmark]
    public void OtlpTraceExporter_Http()
    {
        this.exporter!.Export(new Batch<Activity>(this.activityBatch!, 1));
    }

    [Benchmark]
    public void OtlpTraceExporter_Grpc()
    {
        this.exporter!.Export(new Batch<Activity>(this.activityBatch!, 1));
    }

    [Benchmark]
    public void ProtobufOtlpTraceExporter_Http()
    {
        this.protobufOtlpTraceExporter!.Export(new Batch<Activity>(this.activityBatch!, 1));
    }

    [Benchmark]
    public void ProtobufOtlpTraceExporter_Grpc()
    {
        this.protobufOtlpTraceExporter!.Export(new Batch<Activity>(this.activityBatch!, 1));
    }

    private sealed class MockTraceService : OtlpCollector.TraceService.TraceServiceBase
    {
        private static OtlpCollector.ExportTraceServiceResponse response = new OtlpCollector.ExportTraceServiceResponse();

        public override Task<OtlpCollector.ExportTraceServiceResponse> Export(OtlpCollector.ExportTraceServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(response);
        }
    }
}
#endif
