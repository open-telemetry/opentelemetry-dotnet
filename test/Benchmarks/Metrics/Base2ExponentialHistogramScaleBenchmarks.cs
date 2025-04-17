// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method           | Scale | Mean     | Error    | StdDev   | Allocated |
|----------------- |------ |---------:|---------:|---------:|----------:|
| HistogramHotPath | -11   | 32.44 ns | 0.196 ns | 0.174 ns |         - |
| HistogramHotPath | 3     | 42.33 ns | 0.158 ns | 0.124 ns |         - |
| HistogramHotPath | 20    | 40.57 ns | 0.363 ns | 0.322 ns |         - |
*/

namespace Benchmarks.Metrics;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class Base2ExponentialHistogramScaleBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private Histogram<long>? histogram;
    private MeterProvider? meterProvider;
    private Meter? meter;

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
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void HistogramHotPath()
    {
#pragma warning disable CA5394 // Do not use insecure randomness
        this.histogram!.Record(this.random.Next(MaxValue));
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
