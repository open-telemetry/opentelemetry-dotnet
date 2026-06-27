// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 5800H with Radeon Graphics 3.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


| Method         | ViewConfig              | Mean      | Error    | StdDev   |
|--------------- |------------------------ |----------:|---------:|---------:|
| CounterHotPath | NoView                  | 240.39 ns | 2.585 ns | 2.292 ns |
| CounterHotPath | ViewNA                  | 237.82 ns | 3.095 ns | 2.895 ns |
| CounterHotPath | ViewApplied             | 171.97 ns | 2.295 ns | 1.792 ns |
| CounterHotPath | ViewToRename            | 244.03 ns | 3.004 ns | 2.810 ns |
| CounterHotPath | ViewZeroTag             |  45.36 ns | 0.913 ns | 0.937 ns |
| CounterHotPath | ViewWithExcludedTagKeys | 166.62 ns | 1.982 ns | 1.757 ns |
*/

namespace Benchmarks.Metrics;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class MetricsViewBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
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

        /// <summary>
        /// Provider has view registered with keys that have been excluded.
        /// </summary>
        ViewWithExcludedTagKeys,
    }

    [Params(
        ViewConfiguration.NoView,
        ViewConfiguration.ViewNA,
        ViewConfiguration.ViewApplied,
        ViewConfiguration.ViewToRename,
        ViewConfiguration.ViewZeroTag,
        ViewConfiguration.ViewWithExcludedTagKeys)]
    public ViewConfiguration ViewConfig { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.counter = this.meter.CreateCounter<long>("counter");
        this.metrics = [];

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
                .AddView(this.counter.Name, new MetricStreamConfiguration() { TagKeys = [] })
                .AddInMemoryExporter(this.metrics)
                .Build();
        }
        else if (this.ViewConfig == ViewConfiguration.ViewWithExcludedTagKeys)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddView(this.counter.Name, new MetricStreamConfiguration() { ExcludedTagKeys = ["DimName4", "DimName5"] })
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
#pragma warning disable CA5394 // Do not use insecure randomness
            { "DimName1", DimensionValues[random.Next(0, 2)] },
            { "DimName2", DimensionValues[random.Next(0, 2)] },
            { "DimName3", DimensionValues[random.Next(0, 5)] },
            { "DimName4", DimensionValues[random.Next(0, 5)] },
            { "DimName5", DimensionValues[random.Next(0, 10)] },
#pragma warning restore CA5394 // Do not use insecure randomness
        };

        this.counter?.Add(
            100,
            tags);
    }
}
