// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Metrics;

#pragma warning disable CA1001 // Types that own disposable fields should be disposed by GlobalCleanup.
[MemoryDiagnoser]
public class MetricCollectChurnBenchmarks
#pragma warning restore CA1001
{
    private const string ChurnMeterName = "MetricCollectChurnBenchmarks.Churn";
    private const string CleanMeterName = "MetricCollectChurnBenchmarks.Clean";
    private const int ActiveMetricCount = 4;

    private readonly HashSet<string> churnedExportedMetricNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> cleanExportedMetricNames = new(StringComparer.Ordinal);
    private readonly List<Meter> meters = [];
    private Counter<long>[]? churnedCounters;
    private Counter<long>[]? cleanCounters;
    private BaseExportingMetricReader? churnedReader;
    private BaseExportingMetricReader? cleanReader;
    private MeterProvider? churnedProvider;
    private MeterProvider? cleanProvider;

    [Params(16, 256, 1024)]
    public int HistoricalMetricCount { get; set; }

    [Params(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
    public MetricReaderTemporalityPreference TemporalityPreference { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.churnedProvider = this.CreateProvider(ChurnMeterName, this.churnedExportedMetricNames, out var churnedReader, this.HistoricalMetricCount + ActiveMetricCount);
        this.churnedReader = churnedReader;
        this.cleanProvider = this.CreateProvider(CleanMeterName, this.cleanExportedMetricNames, out var cleanReader, ActiveMetricCount);
        this.cleanReader = cleanReader;

        // Publish many instruments together so the reader's high-water index grows
        // before any slots are released. Disposing the meter completes its
        // instruments; the next collection removes them and makes their slots
        // available for reuse.
        var churnMeter = new Meter(ChurnMeterName);
        this.meters.Add(churnMeter);
        for (var i = 0; i < this.HistoricalMetricCount; i++)
        {
            var counter = churnMeter.CreateCounter<long>($"churn_{i}");
            counter.Add(1);
        }

        churnMeter.Dispose();
        this.churnedReader.Collect();
        if (this.churnedExportedMetricNames.Count != this.HistoricalMetricCount)
        {
            throw new InvalidOperationException("The churned provider must export every historical metric before removing the disposed meter's streams.");
        }

        this.churnedExportedMetricNames.Clear();

        // Publish the active workload only after the churned streams have been
        // removed. The same workload is installed in a fresh provider below.
        var activeMeter = new Meter(ChurnMeterName);
        this.meters.Add(activeMeter);
        this.churnedCounters = CreateActiveCounters(activeMeter);
        var cleanMeter = new Meter(CleanMeterName);
        this.meters.Add(cleanMeter);
        this.cleanCounters = CreateActiveCounters(cleanMeter);

        foreach (var counter in this.churnedCounters)
        {
            counter.Add(1);
        }

        foreach (var counter in this.cleanCounters)
        {
            counter.Add(1);
        }

        this.churnedReader.Collect();
        this.cleanReader.Collect();

        // Verify via the exporter that the churned provider retained exactly
        // the active workload after filling, disposing, and recycling slots.
        if (!HasActiveMetricNames(this.churnedExportedMetricNames)
            || !HasActiveMetricNames(this.cleanExportedMetricNames))
        {
            throw new InvalidOperationException("The churned and clean providers must each export the configured active metrics.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var meter in this.meters)
        {
            meter.Dispose();
        }

        this.churnedProvider?.Dispose();
        this.cleanProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool CollectWithCleanProvider()
    {
        return this.cleanReader!.Collect();
    }

    [Benchmark]
    public bool CollectWithHistoricalChurn()
    {
        return this.churnedReader!.Collect();
    }

    private static Counter<long>[] CreateActiveCounters(Meter meter)
    {
        var counters = new Counter<long>[ActiveMetricCount];
        for (var i = 0; i < counters.Length; i++)
        {
            counters[i] = meter.CreateCounter<long>($"active_{i}");
        }

        return counters;
    }

    private static bool HasActiveMetricNames(HashSet<string> exportedMetricNames)
    {
        if (exportedMetricNames.Count != ActiveMetricCount)
        {
            return false;
        }

        for (var i = 0; i < ActiveMetricCount; i++)
        {
            if (!exportedMetricNames.Contains($"active_{i}"))
            {
                return false;
            }
        }

        return true;
    }

    private MeterProvider CreateProvider(string meterName, HashSet<string> exportedMetricNames, out BaseExportingMetricReader reader, int maxMetricStreams)
    {
#pragma warning disable CA2000 // The provider owns and disposes the reader and exporter.
        reader = new BaseExportingMetricReader(new TestExporter<Metric>(batch =>
        {
            foreach (var metric in batch)
            {
                exportedMetricNames.Add(metric.Name);
            }
        }))
        {
            TemporalityPreference = this.TemporalityPreference,
        };
#pragma warning restore CA2000

        return Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetMaxMetricStreams(maxMetricStreams)
            .AddReader(reader)
            .Build();
    }
}
