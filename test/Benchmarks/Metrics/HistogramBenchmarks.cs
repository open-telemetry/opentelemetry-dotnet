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


| Method                      | BoundCount | Mean      | Error     | StdDev    | Median    | Allocated |
|---------------------------- |----------- |----------:|----------:|----------:|----------:|----------:|
| HistogramHotPath            | 10         |  38.36 ns |  0.401 ns |  0.375 ns |  38.28 ns |         - |
| HistogramWith1LabelHotPath  | 10         |  78.66 ns |  0.258 ns |  0.241 ns |  78.67 ns |         - |
| HistogramWith3LabelsHotPath | 10         | 162.22 ns |  0.946 ns |  0.839 ns | 162.03 ns |         - |
| HistogramWith5LabelsHotPath | 10         | 230.90 ns |  1.262 ns |  1.181 ns | 231.12 ns |         - |
| HistogramWith7LabelsHotPath | 10         | 288.06 ns |  1.362 ns |  1.208 ns | 288.01 ns |         - |
| HistogramHotPath            | 49         |  48.23 ns |  0.137 ns |  0.128 ns |  48.22 ns |         - |
| HistogramWith1LabelHotPath  | 49         |  90.52 ns |  0.404 ns |  0.358 ns |  90.47 ns |         - |
| HistogramWith3LabelsHotPath | 49         | 170.17 ns |  0.801 ns |  0.710 ns | 170.07 ns |         - |
| HistogramWith5LabelsHotPath | 49         | 244.93 ns |  3.935 ns |  3.681 ns | 244.85 ns |         - |
| HistogramWith7LabelsHotPath | 49         | 308.28 ns |  5.927 ns |  5.544 ns | 306.44 ns |         - |
| HistogramHotPath            | 50         |  49.22 ns |  0.280 ns |  0.249 ns |  49.25 ns |         - |
| HistogramWith1LabelHotPath  | 50         |  91.70 ns |  0.589 ns |  0.492 ns |  91.68 ns |         - |
| HistogramWith3LabelsHotPath | 50         | 213.74 ns |  4.258 ns |  5.537 ns | 212.26 ns |         - |
| HistogramWith5LabelsHotPath | 50         | 299.59 ns |  5.940 ns | 13.408 ns | 296.23 ns |         - |
| HistogramWith7LabelsHotPath | 50         | 342.75 ns |  5.066 ns |  4.491 ns | 341.84 ns |         - |
| HistogramHotPath            | 1000       |  72.04 ns |  0.723 ns |  0.603 ns |  71.94 ns |         - |
| HistogramWith1LabelHotPath  | 1000       | 118.73 ns |  2.130 ns |  1.992 ns | 117.98 ns |         - |
| HistogramWith3LabelsHotPath | 1000       | 545.71 ns | 10.644 ns | 12.258 ns | 543.55 ns |         - |
| HistogramWith5LabelsHotPath | 1000       | 661.81 ns | 14.837 ns | 42.091 ns | 651.99 ns |         - |
| HistogramWith7LabelsHotPath | 1000       | 709.50 ns | 14.123 ns | 39.368 ns | 698.27 ns |         - |
*/

namespace Benchmarks.Metrics;

public class HistogramBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private readonly string[] dimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];
    private Histogram<long>? histogram;
    private MeterProvider? meterProvider;
    private Meter? meter;
    private double[]? bounds;

    // Note: Values related to `HistogramBuckets.DefaultHistogramCountForBinarySearch`
    [Params(10, 49, 50, 1000)]
    public int BoundCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogram = this.meter.CreateHistogram<long>("histogram");

        // Evenly distribute the bound values over the range [0, MaxValue)
        this.bounds = new double[this.BoundCount];
        for (int i = 0; i < this.bounds.Length; i++)
        {
            this.bounds[i] = i * MaxValue / this.bounds.Length;
        }

        var exportedItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            })
            .AddView(this.histogram.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
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
