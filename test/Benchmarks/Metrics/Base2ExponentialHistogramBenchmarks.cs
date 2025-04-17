// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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


| Method                      | Mean      | Error    | StdDev   | Allocated |
|---------------------------- |----------:|---------:|---------:|----------:|
| HistogramHotPath            |  42.27 ns | 0.298 ns | 0.279 ns |         - |
| HistogramWith1LabelHotPath  |  88.90 ns | 1.103 ns | 1.032 ns |         - |
| HistogramWith3LabelsHotPath | 166.95 ns | 1.759 ns | 1.559 ns |         - |
| HistogramWith5LabelsHotPath | 248.19 ns | 2.523 ns | 2.237 ns |         - |
| HistogramWith7LabelsHotPath | 310.95 ns | 5.627 ns | 4.988 ns |         - |
*/

namespace Benchmarks.Metrics;

public class Base2ExponentialHistogramBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private readonly string[] dimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];
    private Histogram<long>? histogram;
    private MeterProvider? meterProvider;
    private Meter? meter;

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
            .AddView("histogram", new Base2ExponentialBucketHistogramConfiguration())
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
    }

    [Benchmark]
    public void HistogramWith1LabelHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        this.histogram!.Record(this.random.Next(MaxValue), tag1);
    }

    [Benchmark]
    public void HistogramWith3LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
        this.histogram!.Record(this.random.Next(MaxValue), tag1, tag2, tag3);
    }

    [Benchmark]
    public void HistogramWith5LabelsHotPath()
    {
        var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
            };
        this.histogram!.Record(this.random.Next(MaxValue), tags);
    }

    [Benchmark]
    public void HistogramWith7LabelsHotPath()
    {
        var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName7", this.dimensionValues[this.random.Next(0, 1)] },
            };
        this.histogram!.Record(this.random.Next(MaxValue), tags);
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
