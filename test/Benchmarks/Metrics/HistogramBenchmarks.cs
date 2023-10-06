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


|                      Method | BoundCount |      Mean |     Error |    StdDev |    Median | Allocated |
|---------------------------- |----------- |----------:|----------:|----------:|----------:|----------:|
|            HistogramHotPath |         10 |  50.11 ns |  0.219 ns |  0.204 ns |  50.06 ns |         - |
|  HistogramWith1LabelHotPath |         10 | 108.52 ns |  0.559 ns |  0.523 ns | 108.44 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 212.31 ns |  1.873 ns |  1.661 ns | 212.32 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 299.97 ns |  5.162 ns |  5.737 ns | 298.60 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 349.53 ns |  2.115 ns |  1.875 ns | 349.62 ns |         - |
|            HistogramHotPath |         49 |  61.34 ns |  0.171 ns |  0.160 ns |  61.35 ns |         - |
|  HistogramWith1LabelHotPath |         49 | 118.64 ns |  1.539 ns |  1.285 ns | 118.09 ns |         - |
| HistogramWith3LabelsHotPath |         49 | 226.70 ns |  1.653 ns |  1.465 ns | 226.62 ns |         - |
| HistogramWith5LabelsHotPath |         49 | 314.40 ns |  5.185 ns |  4.850 ns | 313.96 ns |         - |
| HistogramWith7LabelsHotPath |         49 | 375.37 ns |  5.796 ns |  5.138 ns | 373.76 ns |         - |
|            HistogramHotPath |         50 |  60.08 ns |  0.062 ns |  0.049 ns |  60.08 ns |         - |
|  HistogramWith1LabelHotPath |         50 | 118.16 ns |  0.640 ns |  0.568 ns | 118.03 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 258.96 ns |  3.710 ns |  3.098 ns | 259.70 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 353.81 ns |  5.646 ns |  5.281 ns | 351.81 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 406.75 ns |  6.491 ns |  6.072 ns | 406.01 ns |         - |
|            HistogramHotPath |       1000 |  86.82 ns |  0.543 ns |  0.481 ns |  86.68 ns |         - |
|  HistogramWith1LabelHotPath |       1000 | 147.04 ns |  0.535 ns |  0.447 ns | 146.88 ns |         - |
| HistogramWith3LabelsHotPath |       1000 | 619.11 ns | 10.943 ns | 14.608 ns | 617.10 ns |         - |
| HistogramWith5LabelsHotPath |       1000 | 759.64 ns | 22.509 ns | 63.855 ns | 737.58 ns |         - |
| HistogramWith7LabelsHotPath |       1000 | 760.85 ns |  6.220 ns |  5.514 ns | 761.68 ns |         - |
*/

namespace Benchmarks.Metrics;

public class HistogramBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private Histogram<long> histogram;
    private MeterProvider provider;
    private Meter meter;
    private double[] bounds;

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

        this.provider = Sdk.CreateMeterProviderBuilder()
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
