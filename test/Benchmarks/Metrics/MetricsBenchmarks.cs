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


| Method                    | AggregationTemporality | Mean      | Error    | StdDev   | Allocated |
|-------------------------- |----------------------- |----------:|---------:|---------:|----------:|
| CounterHotPath            | Cumulative             |  15.37 ns | 0.072 ns | 0.064 ns |         - |
| CounterWith1LabelsHotPath | Cumulative             |  60.67 ns | 0.764 ns | 0.677 ns |         - |
| CounterWith3LabelsHotPath | Cumulative             | 112.98 ns | 0.843 ns | 0.704 ns |         - |
| CounterWith5LabelsHotPath | Cumulative             | 196.83 ns | 1.632 ns | 1.447 ns |         - |
| CounterWith6LabelsHotPath | Cumulative             | 225.96 ns | 2.676 ns | 2.503 ns |         - |
| CounterWith7LabelsHotPath | Cumulative             | 249.96 ns | 3.459 ns | 3.066 ns |         - |
| CounterHotPath            | Delta                  |  19.83 ns | 0.158 ns | 0.148 ns |         - |
| CounterWith1LabelsHotPath | Delta                  |  59.88 ns | 0.251 ns | 0.235 ns |         - |
| CounterWith3LabelsHotPath | Delta                  | 124.24 ns | 1.490 ns | 1.394 ns |         - |
| CounterWith5LabelsHotPath | Delta                  | 203.75 ns | 3.755 ns | 5.504 ns |         - |
| CounterWith6LabelsHotPath | Delta                  | 226.50 ns | 2.036 ns | 1.805 ns |         - |
| CounterWith7LabelsHotPath | Delta                  | 253.83 ns | 1.247 ns | 0.973 ns |         - |
*/

namespace Benchmarks.Metrics;

public class MetricsBenchmarks
{
    private readonly Random random = new();
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private Counter<long> counter;
    private MeterProvider meterProvider;
    private Meter meter;

    [Params(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
    public MetricReaderTemporalityPreference AggregationTemporality { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());

        var exportedItems = new List<Metric>();
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
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
        this.meterProvider.Dispose();
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
