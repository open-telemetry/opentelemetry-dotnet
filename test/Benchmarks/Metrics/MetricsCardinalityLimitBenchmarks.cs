// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Metrics;

/// <summary>
/// Measures recording a measurement with an unseen tag set once the stream is at
/// its cardinality limit. The reader never collects, so delta never reclaims and
/// the store stays full.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class MetricsCardinalityLimitBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const int CardinalityLimit = 100;
    private const int UnseenPoolSize = 10_000;

    private readonly string[] seenValues = new string[CardinalityLimit];
    private readonly string[] unseenValues = new string[UnseenPoolSize];
    private Counter<long>? counter;
    private MeterProvider? meterProvider;
    private Meter? meter;
    private int next;

    [Params(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
    public MetricReaderTemporalityPreference AggregationTemporality { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < this.seenValues.Length; i++)
        {
            this.seenValues[i] = "seen-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var i = 0; i < this.unseenValues.Length; i++)
        {
            this.unseenValues[i] = "unseen-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        this.meter = new Meter(Utils.GetCurrentMethodName());

        // Both are owned and disposed by the MeterProvider.
#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new TestExporter<Metric>(static _ => { });
        var reader = new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = this.AggregationTemporality,
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddView("counter", new MetricStreamConfiguration { CardinalityLimit = CardinalityLimit })
            .AddReader(reader)
            .Build();

        this.counter = this.meter.CreateCounter<long>("counter");

        // Fill the stream to its cardinality limit.
        for (var i = 0; i < this.seenValues.Length; i++)
        {
            this.counter.Add(1, new KeyValuePair<string, object?>("DimName1", this.seenValues[i]));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void ExistingTagSet1Tag()
    {
        var value = this.seenValues[this.next++ % CardinalityLimit];
        this.counter!.Add(1, new KeyValuePair<string, object?>("DimName1", value));
    }

    [Benchmark]
    public void UnseenTagSetAtLimit1Tag()
    {
        var value = this.unseenValues[this.next++ % UnseenPoolSize];
        this.counter!.Add(1, new KeyValuePair<string, object?>("DimName1", value));
    }

    [Benchmark]
    public void UnseenTagSetAtLimit2Tags()
    {
        var value = this.unseenValues[this.next++ % UnseenPoolSize];
        this.counter!.Add(
            1,
            new KeyValuePair<string, object?>("DimName1", value),
            new KeyValuePair<string, object?>("DimName2", "fixed"));
    }
}
