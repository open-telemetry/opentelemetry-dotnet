// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricDisposeTests : MetricTestsBase
{
    [Fact]
    public void DisposingOneMeterWithSameNameDoesNotDeactivateSharedMetric()
    {
        var exportedItems = new List<Metric>();

        using var m1 = new Meter("shared-meter");
        using var m2 = new Meter("shared-meter");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter("shared-meter")
            .AddInMemoryExporter(exportedItems));

        var counter1 = m1.CreateCounter<long>("my-counter");
        var counter2 = m2.CreateCounter<long>("my-counter");

        counter1.Add(10, new KeyValuePair<string, object?>("key", "value1"));
        counter2.Add(20, new KeyValuePair<string, object?>("key", "value2"));

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);

        var sumValue1Before = GetSumForTag(exportedItems, "value1");
        Assert.Equal(10, sumValue1Before);

        exportedItems.Clear();

        m2.Dispose();

        counter1.Add(5, new KeyValuePair<string, object?>("key", "value1"));

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);

        var sumValue1After = GetSumForTag(exportedItems, "value1");
        Assert.True(sumValue1After == sumValue1Before + 5, $"Expected sum for 'value1' to increase after m2 disposed. Before={sumValue1Before}, After={sumValue1After}");
    }

    [Fact]
    public void DisposingAllMetersWithSameNameDeactivatesSharedMetric()
    {
        var exportedItems = new List<Metric>();

        using var m1 = new Meter("shared-meter-all");
        using var m2 = new Meter("shared-meter-all");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter("shared-meter-all")
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            }));

        var counter1 = m1.CreateCounter<long>("my-counter");
        var counter2 = m2.CreateCounter<long>("my-counter");

        counter1.Add(10, new KeyValuePair<string, object?>("key", "value1"));
        counter2.Add(20, new KeyValuePair<string, object?>("key", "value2"));

        meterProvider.ForceFlush();
        Assert.Single(exportedItems);

        exportedItems.Clear();

        m1.Dispose();
        m2.Dispose();

        counter1.Add(5, new KeyValuePair<string, object?>("key", "value1"));
        counter2.Add(5, new KeyValuePair<string, object?>("key", "value2"));

        meterProvider.ForceFlush();

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void DisposingOneMeterWithMultipleReadersDoesNotDeactivateSharedMetric()
    {
        var exportedItems1 = new List<Metric>();
        var exportedItems2 = new List<Metric>();

        using var m1 = new Meter("multi-reader-meter");
        using var m2 = new Meter("multi-reader-meter");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter("multi-reader-meter")
            .AddInMemoryExporter(exportedItems1)
            .AddInMemoryExporter(exportedItems2));

        var counter1 = m1.CreateCounter<long>("my-counter");
        var counter2 = m2.CreateCounter<long>("my-counter");

        counter1.Add(10, new KeyValuePair<string, object?>("key", "value1"));
        counter2.Add(20, new KeyValuePair<string, object?>("key", "value2"));

        meterProvider.ForceFlush();

        Assert.Single(exportedItems1);
        Assert.Single(exportedItems2);

        var sumValue1Before = GetSumForTag(exportedItems1, "value1");
        Assert.Equal(10, sumValue1Before);

        exportedItems1.Clear();
        exportedItems2.Clear();

        m2.Dispose();

        counter1.Add(5, new KeyValuePair<string, object?>("key", "value1"));

        meterProvider.ForceFlush();

        Assert.Single(exportedItems1);
        Assert.Single(exportedItems2);

        var sumReader1 = GetSumForTag(exportedItems1, "value1");
        var sumReader2 = GetSumForTag(exportedItems2, "value1");

        Assert.Equal(15, sumReader1);
        Assert.Equal(15, sumReader2);
    }

    private static long GetSumForTag(List<Metric> exportedItems, string tagValue)
    {
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            foreach (var kv in mp.Tags)
            {
                if (kv.Key == "key" && kv.Value?.ToString() == tagValue)
                {
                    return mp.GetSumLong();
                }
            }
        }

        return 0;
    }
}
