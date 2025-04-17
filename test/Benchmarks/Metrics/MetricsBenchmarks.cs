// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


| Method                                | AggregationTemporality | Mean      | Error    | StdDev    | Gen0   | Allocated |
|-------------------------------------- |----------------------- |----------:|---------:|----------:|-------:|----------:|
| CounterHotPath                        | Cumulative             |  11.27 ns | 0.173 ns |  0.145 ns |      - |         - |
| CounterWith1LabelsHotPath             | Cumulative             |  43.72 ns | 0.266 ns |  0.248 ns |      - |         - |
| CounterWith2LabelsHotPath             | Cumulative             |  65.90 ns | 1.334 ns |  1.248 ns |      - |         - |
| CounterWith3LabelsHotPath             | Cumulative             |  88.97 ns | 1.785 ns |  1.984 ns |      - |         - |
| CounterWith4LabelsHotPath             | Cumulative             | 122.00 ns | 2.131 ns |  1.994 ns | 0.0138 |      88 B |
| CounterWith5LabelsHotPath             | Cumulative             | 146.73 ns | 2.557 ns |  2.392 ns | 0.0165 |     104 B |
| CounterWith6LabelsHotPath             | Cumulative             | 163.22 ns | 2.136 ns |  1.783 ns | 0.0191 |     120 B |
| CounterWith7LabelsHotPath             | Cumulative             | 184.53 ns | 1.324 ns |  1.238 ns | 0.0215 |     136 B |
| CounterWith1LabelsHotPathUsingTagList | Cumulative             |  58.27 ns | 1.157 ns |  1.504 ns |      - |         - |
| CounterWith2LabelsHotPathUsingTagList | Cumulative             |  92.87 ns | 0.550 ns |  0.488 ns |      - |         - |
| CounterWith3LabelsHotPathUsingTagList | Cumulative             | 120.31 ns | 0.739 ns |  0.617 ns |      - |         - |
| CounterWith4LabelsHotPathUsingTagList | Cumulative             | 132.98 ns | 2.181 ns |  2.240 ns |      - |         - |
| CounterWith5LabelsHotPathUsingTagList | Cumulative             | 154.56 ns | 2.685 ns |  2.380 ns |      - |         - |
| CounterWith6LabelsHotPathUsingTagList | Cumulative             | 171.36 ns | 2.738 ns |  2.286 ns |      - |         - |
| CounterWith7LabelsHotPathUsingTagList | Cumulative             | 194.81 ns | 1.894 ns |  1.582 ns |      - |         - |
| CounterWith8LabelsHotPathUsingTagList | Cumulative             | 214.39 ns | 1.339 ns |  1.187 ns |      - |         - |
| CounterWith9LabelsHotPathUsingTagList | Cumulative             | 300.38 ns | 3.945 ns |  3.690 ns | 0.0710 |     448 B |
| CounterHotPath                        | Delta                  |  14.11 ns | 0.257 ns |  0.228 ns |      - |         - |
| CounterWith1LabelsHotPath             | Delta                  |  49.15 ns | 0.295 ns |  0.246 ns |      - |         - |
| CounterWith2LabelsHotPath             | Delta                  |  68.99 ns | 0.477 ns |  0.398 ns |      - |         - |
| CounterWith3LabelsHotPath             | Delta                  |  93.35 ns | 1.294 ns |  1.080 ns |      - |         - |
| CounterWith4LabelsHotPath             | Delta                  | 141.40 ns | 2.846 ns |  6.539 ns | 0.0138 |      88 B |
| CounterWith5LabelsHotPath             | Delta                  | 163.34 ns | 3.189 ns |  3.917 ns | 0.0165 |     104 B |
| CounterWith6LabelsHotPath             | Delta                  | 181.62 ns | 3.582 ns |  4.125 ns | 0.0191 |     120 B |
| CounterWith7LabelsHotPath             | Delta                  | 201.33 ns | 2.700 ns |  2.108 ns | 0.0215 |     136 B |
| CounterWith1LabelsHotPathUsingTagList | Delta                  |  75.56 ns | 1.457 ns |  1.496 ns |      - |         - |
| CounterWith2LabelsHotPathUsingTagList | Delta                  |  91.48 ns | 1.852 ns |  2.714 ns |      - |         - |
| CounterWith3LabelsHotPathUsingTagList | Delta                  | 129.23 ns | 2.608 ns |  3.298 ns |      - |         - |
| CounterWith4LabelsHotPathUsingTagList | Delta                  | 150.55 ns | 2.433 ns |  2.498 ns |      - |         - |
| CounterWith5LabelsHotPathUsingTagList | Delta                  | 191.60 ns | 3.119 ns |  2.918 ns |      - |         - |
| CounterWith6LabelsHotPathUsingTagList | Delta                  | 196.49 ns | 2.874 ns |  2.400 ns |      - |         - |
| CounterWith7LabelsHotPathUsingTagList | Delta                  | 224.42 ns | 4.482 ns |  8.196 ns |      - |         - |
| CounterWith8LabelsHotPathUsingTagList | Delta                  | 243.75 ns | 4.861 ns |  9.482 ns |      - |         - |
| CounterWith9LabelsHotPathUsingTagList | Delta                  | 331.22 ns | 6.493 ns | 11.373 ns | 0.0710 |     448 B |
*/

namespace Benchmarks.Metrics;

public class MetricsBenchmarks
{
    private readonly Random random = new();
    private readonly string[] dimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];
    private Counter<long>? counter;
    private MeterProvider? meterProvider;
    private Meter? meter;

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
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void CounterHotPath()
    {
        this.counter!.Add(100);
    }

    [Benchmark]
    public void CounterWith1LabelsHotPath()
    {
#pragma warning disable CA5394 // Do not use insecure randomness
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter!.Add(100, tag1);
    }

    [Benchmark]
    public void CounterWith2LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter!.Add(100, tag1, tag2);
    }

    [Benchmark]
    public void CounterWith3LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter!.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void CounterWith4LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 5)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
        var tag4 = new KeyValuePair<string, object?>("DimName4", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter!.Add(100, tag1, tag2, tag3, tag4);
    }

    [Benchmark]
    public void CounterWith5LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 5)]);
        var tag4 = new KeyValuePair<string, object?>("DimName4", this.dimensionValues[this.random.Next(0, 5)]);
        var tag5 = new KeyValuePair<string, object?>("DimName5", this.dimensionValues[this.random.Next(0, 10)]);
        this.counter!.Add(100, tag1, tag2, tag3, tag4, tag5);
    }

    [Benchmark]
    public void CounterWith6LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 2)]);
        var tag4 = new KeyValuePair<string, object?>("DimName4", this.dimensionValues[this.random.Next(0, 5)]);
        var tag5 = new KeyValuePair<string, object?>("DimName5", this.dimensionValues[this.random.Next(0, 5)]);
        var tag6 = new KeyValuePair<string, object?>("DimName6", this.dimensionValues[this.random.Next(0, 5)]);
        this.counter!.Add(100, tag1, tag2, tag3, tag4, tag5, tag6);
    }

    [Benchmark]
    public void CounterWith7LabelsHotPath()
    {
        var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 1)]);
        var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
        var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 2)]);
        var tag4 = new KeyValuePair<string, object?>("DimName4", this.dimensionValues[this.random.Next(0, 2)]);
        var tag5 = new KeyValuePair<string, object?>("DimName5", this.dimensionValues[this.random.Next(0, 5)]);
        var tag6 = new KeyValuePair<string, object?>("DimName6", this.dimensionValues[this.random.Next(0, 5)]);
        var tag7 = new KeyValuePair<string, object?>("DimName7", this.dimensionValues[this.random.Next(0, 5)]);
        this.counter!.Add(100, tag1, tag2, tag3, tag4, tag5, tag6, tag7);
    }

    [Benchmark]
    public void CounterWith1LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith2LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 10)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith3LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 10)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 10)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith4LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 10)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith5LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith6LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName6", this.dimensionValues[this.random.Next(0, 5)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith7LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName6", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName7", this.dimensionValues[this.random.Next(0, 5)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith8LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName6", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName7", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName8", this.dimensionValues[this.random.Next(0, 5)] },
        };
        this.counter!.Add(100, tags);
    }

    [Benchmark]
    public void CounterWith9LabelsHotPathUsingTagList()
    {
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName2", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName3", this.dimensionValues[this.random.Next(0, 1)] },
            { "DimName4", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName5", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
            { "DimName7", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName8", this.dimensionValues[this.random.Next(0, 5)] },
            { "DimName9", this.dimensionValues[this.random.Next(0, 5)] },
#pragma warning restore CA5394 // Do not use insecure randomness
        };
        this.counter!.Add(100, tags);
    }
}
