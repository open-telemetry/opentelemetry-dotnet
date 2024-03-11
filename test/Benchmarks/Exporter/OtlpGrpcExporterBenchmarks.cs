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
using OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Trace.V1;

namespace Benchmarks.Exporter;

public class OtlpGrpcExporterBenchmarks
{
    private OtlpTraceExporter exporter;
    private Activity activity;
    private CircularBuffer<Activity> activityBatch;

    [Params(1, 10, 100)]
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
            new OtlpExporterTransmissionHandler<ExportTraceServiceRequest>(new OtlpGrpcTraceExportClient(options, new TestTraceServiceClient())));

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.exporter.Shutdown();
        this.exporter.Dispose();
    }

    [Benchmark]
    public void OtlpExporter_Batching()
    {
        for (int i = 0; i < this.NumberOfBatches; i++)
        {
            for (int c = 0; c < this.NumberOfSpans; c++)
            {
                this.activityBatch.Add(this.activity);
            }

            this.exporter.Export(new Batch<Activity>(this.activityBatch, this.NumberOfSpans));
        }
    }
}
