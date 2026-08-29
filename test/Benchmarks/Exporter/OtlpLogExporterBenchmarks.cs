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

    [Params(1, 512, 2048)]
    public int BatchSize { get; set; }

    [GlobalSetup(Target = nameof(OtlpLogExporter_Grpc))]
    public void GlobalSetupGrpc()
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddGrpc();

        appBuilder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(
                4317,
                listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
        });

        var app = appBuilder.Build();

        app.MapGrpcService<MockLogService>();

        app.Start();

        this.host = app;

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpLogExporter(options);

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(this.BatchSize);
        for (var i = 0; i < this.BatchSize; i++)
        {
            this.logRecordBatch.Add(this.logRecord);
        }
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
        this.logRecordBatch = new CircularBuffer<LogRecord>(this.BatchSize);
        for (var i = 0; i < this.BatchSize; i++)
        {
            this.logRecordBatch.Add(this.logRecord);
        }
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
        this.exporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, this.BatchSize));
    }

    [Benchmark]
    public void OtlpLogExporter_Grpc()
    {
        this.exporter!.Export(new Batch<LogRecord>(this.logRecordBatch!, this.BatchSize));
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class MockLogService : OtlpCollector.LogsService.LogsServiceBase
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        private static readonly OtlpCollector.ExportLogsServiceResponse Response = new();

        public override Task<OtlpCollector.ExportLogsServiceResponse> Export(OtlpCollector.ExportLogsServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(Response);
        }
    }
}
#endif
