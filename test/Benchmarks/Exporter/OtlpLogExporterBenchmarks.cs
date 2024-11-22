// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OtlpCollector = OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Logs.V1;

namespace Benchmarks.Exporter;

[MemoryDiagnoser]
public class OtlpLogExporterBenchmarks
{
    private OtlpLogExporter? exporter;
    private ProtobufOtlpLogExporter? protobufOtlpLogExporter;
    private LogRecord? logRecord;
    private CircularBuffer<LogRecord>? logRecordBatch;

    private IHost? host;
    private IDisposable? server;
    private string? serverHost;
    private int serverPort;

    [GlobalSetup(Target = nameof(OtlpLogExporter_Grpc))]
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
                      endpoints.MapGrpcService<MockLogService>();
                  });
              }))
          .Start();

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpLogExporter(options);

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
        this.logRecordBatch.Add(this.logRecord);
    }

    [GlobalSetup(Target = nameof(OtlpLogExporter_Http))]
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
        this.exporter = new OtlpLogExporter(options);

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
        this.logRecordBatch.Add(this.logRecord);
    }

    [GlobalSetup(Target = nameof(ProtobufOtlpLogExporter_Grpc))]
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
                      endpoints.MapGrpcService<MockLogService>();
                  });
              }))
          .Start();

        var options = new OtlpExporterOptions();
        this.protobufOtlpLogExporter = new ProtobufOtlpLogExporter(options);

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
        this.logRecordBatch.Add(this.logRecord);
    }

    [GlobalSetup(Target = nameof(ProtobufOtlpLogExporter_Http))]
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
        this.protobufOtlpLogExporter = new ProtobufOtlpLogExporter(options);

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
        this.logRecordBatch.Add(this.logRecord);
    }

    [GlobalCleanup(Target = nameof(OtlpLogExporter_Grpc))]
    public void GlobalCleanupGrpc()
    {
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
        this.host?.Dispose();
    }

    [GlobalCleanup(Target = nameof(OtlpLogExporter_Http))]
    public void GlobalCleanupHttp()
    {
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
        this.server?.Dispose();
    }

    [GlobalCleanup(Target = nameof(ProtobufOtlpLogExporter_Grpc))]
    public void GlobalCleanupProtobufGrpc()
    {
        this.protobufOtlpLogExporter?.Shutdown();
        this.protobufOtlpLogExporter?.Dispose();
        this.host?.Dispose();
    }

    [GlobalCleanup(Target = nameof(ProtobufOtlpLogExporter_Http))]
    public void GlobalCleanupProtobufHttp()
    {
        this.protobufOtlpLogExporter?.Shutdown();
        this.protobufOtlpLogExporter?.Dispose();
        this.server?.Dispose();
    }

    [Benchmark]
    public void OtlpLogExporter_Http()
    {
        this.exporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, 1));
    }

    [Benchmark]
    public void OtlpLogExporter_Grpc()
    {
        this.exporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, 1));
    }

    [Benchmark]
    public void ProtobufOtlpLogExporter_Http()
    {
        this.protobufOtlpLogExporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, 1));
    }

    [Benchmark]
    public void ProtobufOtlpLogExporter_Grpc()
    {
        this.protobufOtlpLogExporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, 1));
    }

    private sealed class MockLogService : OtlpCollector.LogsService.LogsServiceBase
    {
        private static OtlpCollector.ExportLogsServiceResponse response = new OtlpCollector.ExportLogsServiceResponse();

        public override Task<OtlpCollector.ExportLogsServiceResponse> Export(OtlpCollector.ExportLogsServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(response);
        }
    }
}
#endif
