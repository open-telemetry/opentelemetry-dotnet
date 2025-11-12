// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class OtlpGrpcExporterBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private OtlpTraceExporter? exporter;
    private Activity? activity;
    private CircularBuffer<Activity>? activityBatch;

    [Params(1, 10, 20)]
    public int NumberOfBatches { get; set; }

    [Params(10000)]
    public int NumberOfSpans { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var options = new OtlpExporterOptions();
        this.exporter = new OtlpTraceExporter(
            options,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
#pragma warning disable CA2000 // Dispose objects before losing scope
            new OtlpExporterTransmissionHandler(new OtlpGrpcExportClient(options, options.HttpClientFactory(), "opentelemetry.proto.collector.trace.v1.TraceService/Export"), options.TimeoutMilliseconds));
#pragma warning restore CA2000 // Dispose objects before losing scope

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.activity?.Dispose();
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
    }

    [Benchmark]
    public void OtlpExporter_Batching()
    {
        for (int i = 0; i < this.NumberOfBatches; i++)
        {
            for (int c = 0; c < this.NumberOfSpans; c++)
            {
                this.activityBatch!.Add(this.activity!);
            }

            this.exporter!.Export(new Batch<Activity>(this.activityBatch!, this.NumberOfSpans));
        }
    }
}
