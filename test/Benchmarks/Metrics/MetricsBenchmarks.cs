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


|                    Method | AggregationTemporality |      Mean |    Error |   StdDev | Allocated |
|-------------------------- |----------------------- |----------:|---------:|---------:|----------:|
|            CounterHotPath |             Cumulative |  17.06 ns | 0.113 ns | 0.094 ns |         - |
| CounterWith1LabelsHotPath |             Cumulative |  71.47 ns | 1.464 ns | 2.100 ns |         - |
| CounterWith3LabelsHotPath |             Cumulative | 162.04 ns | 2.469 ns | 2.188 ns |         - |
| CounterWith5LabelsHotPath |             Cumulative | 237.30 ns | 2.884 ns | 2.698 ns |         - |
| CounterWith6LabelsHotPath |             Cumulative | 269.41 ns | 4.087 ns | 3.623 ns |         - |
| CounterWith7LabelsHotPath |             Cumulative | 303.01 ns | 5.313 ns | 4.970 ns |         - |
|            CounterHotPath |                  Delta |  17.30 ns | 0.350 ns | 0.310 ns |         - |
| CounterWith1LabelsHotPath |                  Delta |  70.96 ns | 0.608 ns | 0.539 ns |         - |
| CounterWith3LabelsHotPath |                  Delta | 156.55 ns | 3.139 ns | 3.358 ns |         - |
| CounterWith5LabelsHotPath |                  Delta | 247.14 ns | 4.703 ns | 5.598 ns |         - |
| CounterWith6LabelsHotPath |                  Delta | 271.30 ns | 5.310 ns | 5.215 ns |         - |
| CounterWith7LabelsHotPath |                  Delta | 309.02 ns | 5.934 ns | 5.828 ns |         - |
*/

namespace Benchmarks.Metrics;

public class MetricsBenchmarks
{
    private readonly Random random = new();
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private Counter<long> counter;
    private MeterProvider provider;
    private Meter meter;

    [Params(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
    public MetricReaderTemporalityPreference AggregationTemporality { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());

        var exportedItems = new List<Metric>();
        this.provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                metricReaderOptions.TemporalityPreference = this.AggregationTemporality;
            })
            .Build();

        this.counter = this.meter.CreateCounter<long>("counter");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.provider?.Dispose();
    }

    [Benchmark]
    public void CounterHotPath()
    {
        this.counter.Add(100);
    }

    [Benchmark]
    public void CounterWith1LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        this.counter.Add(100, tag1);
    }

    [Benchmark]
    public void CounterWith3LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void CounterWith5LabelsHotPath()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith6LabelsHotPath()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
        };
        this.counter.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith7LabelsHotPath()
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
        this.counter.Add(100, tags);
    }
}
