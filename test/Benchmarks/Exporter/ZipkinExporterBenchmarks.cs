// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias Zipkin;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Zipkin::OpenTelemetry.Exporter;

namespace Benchmarks.Exporter;

#if !NETFRAMEWORK
[ThreadingDiagnoser]
#endif
public class ZipkinExporterBenchmarks
{
    private readonly byte[] buffer = new byte[4096];
    private Activity? activity;
    private CircularBuffer<Activity>? activityBatch;
    private IDisposable? server;
    private string? serverHost;
    private int serverPort;

    [Params(1, 10, 20)]
    public int NumberOfBatches { get; set; }

    [Params(10000)]
    public int NumberOfSpans { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
        this.server = TestHttpServer.RunServer(
            (ctx) =>
            {
                using (Stream receiveStream = ctx.Request.InputStream)
                {
                    while (true)
                    {
                        if (receiveStream.Read(this.buffer, 0, this.buffer.Length) == 0)
                        {
                            break;
                        }
                    }
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            },
            out this.serverHost,
            out this.serverPort);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.server?.Dispose();
    }

    [Benchmark]
    public void ZipkinExporter_Batching()
    {
        using var exporter = new ZipkinExporter(
            new ZipkinExporterOptions
            {
                Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
            });

        for (int i = 0; i < this.NumberOfBatches; i++)
        {
            for (int c = 0; c < this.NumberOfSpans; c++)
            {
                this.activityBatch!.Add(this.activity!);
            }

            exporter.Export(new Batch<Activity>(this.activityBatch!, this.NumberOfSpans));
        }

        exporter.Shutdown();
    }
}
