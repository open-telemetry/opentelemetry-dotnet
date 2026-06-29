// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class ProtobufOtlpMetricSerializerBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private readonly byte[] buffer = new byte[256 * 1024];
    private Meter? meter;
    private MeterProvider? meterProvider;
    private Metric[]? capturedMetrics;
    private Batch<Metric> batch;

    [Params(1, 4, 16, 64, 256)]
    public int MetricCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var meterName = "benchmark.metric-serializer." + Guid.NewGuid().ToString("N");
        this.meter = new Meter(meterName);

        var collected = new List<Metric>();
#pragma warning disable CA2000 // Ownership transferred to MeterProvider via AddReader.
        var captureExporter = new CaptureExporter(collected);
        var reader = new BaseExportingMetricReader(captureExporter);
#pragma warning restore CA2000

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddReader(reader)
            .Build();

        for (var i = 0; i < this.MetricCount; i++)
        {
            var counter = this.meter.CreateCounter<long>(
                name: "benchmark.requests." + i,
                unit: "requests",
                description: "Number of requests processed by component " + i);
            counter.Add(1);
        }

        this.meterProvider.ForceFlush();

        this.capturedMetrics = collected.ToArray();
        this.batch = new Batch<Metric>(this.capturedMetrics, this.capturedMetrics.Length);
    }

    [Benchmark]
    public int WriteMetricsData()
    {
        var buf = this.buffer;
        return ProtobufOtlpMetricSerializer.WriteMetricsData(ref buf, 0, Resource.Empty, in this.batch);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meterProvider?.Dispose();
        this.meter?.Dispose();
    }

    private sealed class CaptureExporter : BaseExporter<Metric>
    {
        private readonly List<Metric> collected;

        public CaptureExporter(List<Metric> collected)
        {
            this.collected = collected;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                this.collected.Add(metric);
            }

            return ExportResult.Success;
        }
    }
}
