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
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

/*
BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.2134/22H2/2022Update/SunValley2) (Hyper-V)
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 7.0.400
  [Host]     : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2


|                 Method |     Mean |   Error |  StdDev |   Gen0 |   Gen1 | Allocated |
|----------------------- |---------:|--------:|--------:|-------:|-------:|----------:|
| OtlpTraceExporter_Http | 139.4 us | 1.41 us | 1.32 us | 0.4883 | 0.2441 |    9.8 KB |
| OtlpTraceExporter_Grpc | 263.0 us | 3.47 us | 3.24 us | 0.4883 |      - |   9.34 KB |
*/

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class OtlpTraceExporterBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private OtlpTraceExporter? exporter;
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

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class MockTraceService : OtlpCollector.TraceService.TraceServiceBase
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        private static readonly OtlpCollector.ExportTraceServiceResponse Response = new();

        public override Task<OtlpCollector.ExportTraceServiceResponse> Export(OtlpCollector.ExportTraceServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(Response);
        }
    }
}
#endif
