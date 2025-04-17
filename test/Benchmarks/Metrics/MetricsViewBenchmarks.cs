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


| Method         | ViewConfig   | Mean      | Error    | StdDev   | Allocated |
|--------------- |------------- |----------:|---------:|---------:|----------:|
| CounterHotPath | NoView       | 217.94 ns | 3.950 ns | 3.502 ns |         - |
| CounterHotPath | ViewNA       | 206.09 ns | 1.634 ns | 1.364 ns |         - |
| CounterHotPath | ViewApplied  | 210.63 ns | 4.116 ns | 5.904 ns |         - |
| CounterHotPath | ViewToRename | 207.05 ns | 1.592 ns | 1.329 ns |         - |
| CounterHotPath | ViewZeroTag  |  68.67 ns | 0.613 ns | 0.573 ns |         - |
*/

namespace Benchmarks.Metrics;

public class MetricsViewBenchmarks
{
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
    private static readonly string[] DimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];
    private List<Metric>? metrics;
    private Counter<long>? counter;
    private MeterProvider? meterProvider;
    private Meter? meter;

    public enum ViewConfiguration
    {
        /// <summary>
        /// No views registered in the provider.
        /// </summary>
        NoView,

        /// <summary>
        /// Provider has view registered, but it doesn't select the instrument.
        /// This tests the perf impact View has on hot path, for those
        /// instruments not participating in View feature.
        /// </summary>
        ViewNA,

        /// <summary>
        /// Provider has view registered and it does select the instrument
        /// and keeps the subset of tags.
        /// </summary>
        ViewApplied,

        /// <summary>
        /// Provider has view registered and it does select the instrument
        /// and renames.
        /// </summary>
        ViewToRename,

        /// <summary>
        /// Provider has view registered and it does select the instrument
        /// and drops every tag.
        /// </summary>
        ViewZeroTag,
    }

    [Params(
        ViewConfiguration.NoView,
        ViewConfiguration.ViewNA,
        ViewConfiguration.ViewApplied,
        ViewConfiguration.ViewToRename,
        ViewConfiguration.ViewZeroTag)]
    public ViewConfiguration ViewConfig { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.counter = this.meter.CreateCounter<long>("counter");
        this.metrics = new List<Metric>();

        if (this.ViewConfig == ViewConfiguration.NoView)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
        else if (this.ViewConfig == ViewConfiguration.ViewNA)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddView("nomatch", new MetricStreamConfiguration() { TagKeys = ["DimName1", "DimName2", "DimName3"] })
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
        else if (this.ViewConfig == ViewConfiguration.ViewApplied)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddView(this.counter.Name, new MetricStreamConfiguration() { TagKeys = ["DimName1", "DimName2", "DimName3"] })
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
        else if (this.ViewConfig == ViewConfiguration.ViewToRename)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddView(this.counter.Name, "newname")
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
        else if (this.ViewConfig == ViewConfiguration.ViewZeroTag)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddView(this.counter.Name, new MetricStreamConfiguration() { TagKeys = Array.Empty<string>() })
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
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
        var random = ThreadLocalRandom.Value!;
        var tags = new TagList
        {
            { "DimName1", DimensionValues[random.Next(0, 2)] },
            { "DimName2", DimensionValues[random.Next(0, 2)] },
            { "DimName3", DimensionValues[random.Next(0, 5)] },
            { "DimName4", DimensionValues[random.Next(0, 5)] },
            { "DimName5", DimensionValues[random.Next(0, 10)] },
        };

        this.counter?.Add(
            100,
            tags);
    }
}
