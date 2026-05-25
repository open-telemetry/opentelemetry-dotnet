// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

[MemoryDiagnoser]
public class MetricPointAllocationBenchmarks
{
    private const string CounterName = "counter";
    private const string ManyCountersNamePrefix = "counter_many_";
    private const int ManyCountersCount = 100;

    private static readonly KeyValuePair<string, object?>[] LowCardinalityTags =
    [
        new("DimName1", "DimVal1"),
        new("DimName1", "DimVal2"),
        new("DimName1", "DimVal3"),
        new("DimName1", "DimVal4"),
        new("DimName1", "DimVal5"),
        new("DimName1", "DimVal6"),
        new("DimName1", "DimVal7"),
        new("DimName1", "DimVal8"),
        new("DimName1", "DimVal9"),
        new("DimName1", "DimVal10"),
    ];

    [Params(100_000)]
    public int CardinalityLimit { get; set; }

    [Benchmark]
    public void DefaultEagerNoMeasurements()
    {
        this.Run(enableLazyAllocation: false, emittedTimeSeries: 0);
    }

    [Benchmark]
    public void LazyNoMeasurements()
    {
        this.Run(enableLazyAllocation: true, emittedTimeSeries: 0);
    }

    [Benchmark]
    public void DefaultEagerOneTimeSeries()
    {
        this.Run(enableLazyAllocation: false, emittedTimeSeries: 1);
    }

    [Benchmark]
    public void LazyOneTimeSeries()
    {
        this.Run(enableLazyAllocation: true, emittedTimeSeries: 1);
    }

    [Benchmark]
    public void DefaultEagerTenTimeSeries()
    {
        this.Run(enableLazyAllocation: false, emittedTimeSeries: LowCardinalityTags.Length);
    }

    [Benchmark]
    public void LazyTenTimeSeries()
    {
        this.Run(enableLazyAllocation: true, emittedTimeSeries: LowCardinalityTags.Length);
    }

    [Benchmark]
    public void DefaultEagerManyMetricsHighCardinality()
    {
        this.RunManyMetrics(enableLazyAllocation: false);
    }

    [Benchmark]
    public void LazyManyMetricsHighCardinality()
    {
        this.RunManyMetrics(enableLazyAllocation: true);
    }

    private void Run(bool enableLazyAllocation, int emittedTimeSeries)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter(nameof(MetricPointAllocationBenchmarks));
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(CounterName, this.CreateMetricStreamConfiguration(enableLazyAllocation))
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counter = meter.CreateCounter<long>(CounterName);
        for (var i = 0; i < emittedTimeSeries; i++)
        {
            counter.Add(1, LowCardinalityTags[i]);
        }
    }

    private void RunManyMetrics(bool enableLazyAllocation)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter(nameof(MetricPointAllocationBenchmarks));
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView($"{ManyCountersNamePrefix}*", this.CreateMetricStreamConfiguration(enableLazyAllocation))
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counters = new Counter<long>[ManyCountersCount];
        for (var i = 0; i < counters.Length; i++)
        {
            counters[i] = meter.CreateCounter<long>($"{ManyCountersNamePrefix}{i}");
            counters[i].Add(1, LowCardinalityTags[0]);
        }
    }

    private MetricStreamConfiguration CreateMetricStreamConfiguration(bool enableLazyAllocation)
    {
        var configuration = new MetricStreamConfiguration
        {
            CardinalityLimit = this.CardinalityLimit,
        };

        if (enableLazyAllocation)
        {
#pragma warning disable OTEL1006 // Experimental API
            configuration.EnableMetricPointLazyAllocation = true;
#pragma warning restore OTEL1006 // Experimental API
        }

        return configuration;
    }
}
