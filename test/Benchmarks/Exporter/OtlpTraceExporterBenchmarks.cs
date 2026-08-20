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

    [Params(1, 512, 2048)]
    public int BatchSize { get; set; }

    [GlobalSetup(Target = nameof(OtlpTraceExporter_Grpc))]
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

        app.MapGrpcService<MockTraceService>();

        app.Start();

        this.host = app;

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpTraceExporter(options);

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(this.BatchSize);
        for (var i = 0; i < this.BatchSize; i++)
        {
            this.activityBatch.Add(this.activity);
        }
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
        this.activityBatch = new CircularBuffer<Activity>(this.BatchSize);
        for (var i = 0; i < this.BatchSize; i++)
        {
            this.activityBatch.Add(this.activity);
        }
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
        this.exporter!.Export(new Batch<Activity>(this.activityBatch!, this.BatchSize));
    }

    [Benchmark]
    public void OtlpTraceExporter_Grpc()
    {
        this.exporter!.Export(new Batch<Activity>(this.activityBatch!, this.BatchSize));
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
