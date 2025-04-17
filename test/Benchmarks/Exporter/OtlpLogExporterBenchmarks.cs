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
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;

/*
BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.2134/22H2/2022Update/SunValley2) (Hyper-V)
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 7.0.400
  [Host]     : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2


|               Method |     Mean |   Error |  StdDev |   Gen0 |   Gen1 | Allocated |
|--------------------- |---------:|--------:|--------:|-------:|-------:|----------:|
| OtlpLogExporter_Http | 138.7 us | 2.08 us | 1.95 us | 0.4883 | 0.2441 |   9.85 KB |
| OtlpLogExporter_Grpc | 268.3 us | 2.57 us | 2.28 us | 0.4883 |      - |   9.54 KB |
*/

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class OtlpLogExporterBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private OtlpLogExporter? exporter;
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
