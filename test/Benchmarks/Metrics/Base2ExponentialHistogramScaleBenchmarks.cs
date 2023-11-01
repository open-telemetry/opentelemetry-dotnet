// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.5, OS=macOS Ventura 13.4 (22F66) [Darwin 22.5.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD


|           Method | Scale |     Mean |    Error |   StdDev | Allocated |
|----------------- |------ |---------:|---------:|---------:|----------:|
| HistogramHotPath |   -11 | 29.79 ns | 0.054 ns | 0.042 ns |         - |
| HistogramHotPath |     3 | 32.10 ns | 0.086 ns | 0.080 ns |         - |
| HistogramHotPath |    20 | 32.08 ns | 0.076 ns | 0.063 ns |         - |
*/

namespace Benchmarks.Metrics;

public class Base2ExponentialHistogramScaleBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private Histogram<long> histogram;
    private MeterProvider meterProvider;
    private Meter meter;

    // This is a simple benchmark that records values in the range [0, 10000].
    // The reason the following scales are benchmarked are as follows:
    //
    // -11: Non-positive scales should perform better than positive scales.
    //      The algorithm to map values to buckets for non-positive scales is more efficient.
    //   3: The benchmark records values in the range [0, 10000] and uses the default max number of buckets (160).
    //      Scale 3 is the maximum scale that will fit this range of values given the number of buckets.
    //      That is, no scale down will occur.
    //  20: Scale 20 should perform the same as scale 3. During warmup the histogram should scale down to 3.
    [Params(-11, 3, 20)]
    public int Scale { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogram = this.meter.CreateHistogram<long>("histogram");

        var exportedItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            })
            .AddView("histogram", new Base2ExponentialBucketHistogramConfiguration() { MaxScale = this.Scale, MaxSize = Metric.DefaultExponentialHistogramMaxBuckets })
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.meterProvider.Dispose();
    }

    [Benchmark]
    public void HistogramHotPath()
    {
        this.histogram.Record(this.random.Next(MaxValue));
    }
}
