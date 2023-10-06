// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|                      Method |      Mean |    Error |   StdDev | Allocated |
|---------------------------- |----------:|---------:|---------:|----------:|
|            HistogramHotPath |  54.78 ns | 0.907 ns | 0.848 ns |         - |
|  HistogramWith1LabelHotPath | 115.37 ns | 0.388 ns | 0.363 ns |         - |
| HistogramWith3LabelsHotPath | 228.03 ns | 3.767 ns | 3.146 ns |         - |
| HistogramWith5LabelsHotPath | 316.60 ns | 5.980 ns | 9.311 ns |         - |
| HistogramWith7LabelsHotPath | 366.86 ns | 2.694 ns | 3.596 ns |         - |
*/

namespace Benchmarks.Metrics;

public class Base2ExponentialHistogramBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private Histogram<long> histogram;
    private MeterProvider provider;
    private Meter meter;

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogram = this.meter.CreateHistogram<long>("histogram");

        var exportedItems = new List<Metric>();

        this.provider = Sdk.CreateMeterProviderBuilder()
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
        this.provider?.Dispose();
    }

    [Benchmark]
    public void HistogramHotPath()
    {
        this.histogram.Record(this.random.Next(MaxValue));
    }

    [Benchmark]
    public void HistogramWith1LabelHotPath()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        this.histogram.Record(this.random.Next(MaxValue), tag1);
    }

    [Benchmark]
    public void HistogramWith3LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
        this.histogram.Record(this.random.Next(MaxValue), tag1, tag2, tag3);
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
        this.histogram.Record(this.random.Next(MaxValue), tags);
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
        this.histogram.Record(this.random.Next(MaxValue), tags);
    }
}
