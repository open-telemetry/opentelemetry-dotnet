// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class MetricOverflowAttributeTests
{
    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForCounter(MetricReaderTemporalityPreference temporalityPreference)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        // There are two reserved MetricPoints
        // 1. For zero tags
        // 2. For metric overflow attribute when user opts-in for this feature

        counter.Add(10); // Record measurement for zero tags

        // Max number for MetricPoints available for use when emitted with tags
        var maxMetricPointsForUse = MeterProviderBuilderSdk.DefaultCardinalityLimit;

        for (var i = 0; i < maxMetricPointsForUse; i++)
        {
            // Emit unique key-value pairs to use up the available MetricPoints
            // Once this loop is run, we have used up all available MetricPoints for metrics emitted with tags
            counter.Add(10, new KeyValuePair<string, object?>("Key", i));
        }

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
        var metric = exportedItems[0];

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        MetricPoint overflowMetricPoint;

        // We still have not exceeded the max MetricPoint limit
        Assert.DoesNotContain(metricPoints, mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

        exportedItems.Clear();
        metricPoints.Clear();

        counter.Add(5, new KeyValuePair<string, object?>("Key", 2000)); // Emit a metric to exceed the max MetricPoint limit

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        MetricPoint zeroTagsMetricPoint;
        if (temporalityPreference == MetricReaderTemporalityPreference.Cumulative)
        {
            // Check metric point for zero tags
            zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);
            Assert.Equal(10, zeroTagsMetricPoint.GetSumLong());
        }

        // Check metric point for overflow
        overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
        Assert.Equal(true, overflowMetricPoint.Tags.KeyAndValues[0].Value);
        Assert.Equal(1, overflowMetricPoint.Tags.Count);
        Assert.Equal(5, overflowMetricPoint.GetSumLong());

        exportedItems.Clear();
        metricPoints.Clear();

        counter.Add(15); // Record another measurement for zero tags

        // Emit 2500 more newer MetricPoints with distinct dimension combinations
        for (var i = 2001; i < 4501; i++)
        {
            counter.Add(5, new KeyValuePair<string, object?>("Key", i));
        }

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);
        overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

        if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
        {
            Assert.Equal(15, zeroTagsMetricPoint.GetSumLong());

            int expectedSum;

            // Number of metric points that were available before the 2500 measurements were made = 2000 (max MetricPoints)
            // Because unused metric points are reclaimed, number of metric points dropped = 2500 - 2000 = 500
            expectedSum = 2500; // 500 * 5

            Assert.Equal(expectedSum, overflowMetricPoint.GetSumLong());
        }
        else
        {
            Assert.Equal(25, zeroTagsMetricPoint.GetSumLong());
            Assert.Equal(12505, overflowMetricPoint.GetSumLong()); // 5 + (2500 * 5)
        }

        exportedItems.Clear();
        metricPoints.Clear();

        // Test that the SDK continues to correctly aggregate the previously registered measurements even after overflow has occurred
        counter.Add(25);

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);

        if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
        {
            Assert.Equal(25, zeroTagsMetricPoint.GetSumLong());
        }
        else
        {
            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            Assert.Equal(50, zeroTagsMetricPoint.GetSumLong());
            Assert.Equal(12505, overflowMetricPoint.GetSumLong());
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForHistogram(MetricReaderTemporalityPreference temporalityPreference)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var histogram = meter.CreateHistogram<long>("TestHistogram");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        // There are two reserved MetricPoints
        // 1. For zero tags
        // 2. For metric overflow attribute when user opts-in for this feature

        histogram.Record(10); // Record measurement for zero tags

        // Max number for MetricPoints available for use when emitted with tags
        var maxMetricPointsForUse = MeterProviderBuilderSdk.DefaultCardinalityLimit;

        for (var i = 0; i < maxMetricPointsForUse; i++)
        {
            // Emit unique key-value pairs to use up the available MetricPoints
            // Once this loop is run, we have used up all available MetricPoints for metrics emitted with tags
            histogram.Record(10, new KeyValuePair<string, object?>("Key", i));
        }

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
        var metric = exportedItems[0];

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        MetricPoint overflowMetricPoint;

        // We still have not exceeded the max MetricPoint limit
        Assert.DoesNotContain(metricPoints, mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

        exportedItems.Clear();
        metricPoints.Clear();

        histogram.Record(5, new KeyValuePair<string, object?>("Key", 2000)); // Emit a metric to exceed the max MetricPoint limit

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        MetricPoint zeroTagsMetricPoint;
        if (temporalityPreference == MetricReaderTemporalityPreference.Cumulative)
        {
            // Check metric point for zero tags
            zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);
            Assert.Equal(10, zeroTagsMetricPoint.GetHistogramSum());
        }

        // Check metric point for overflow
        overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
        Assert.Equal(true, overflowMetricPoint.Tags.KeyAndValues[0].Value);
        Assert.Equal(1, overflowMetricPoint.Tags.Count);
        Assert.Equal(5, overflowMetricPoint.GetHistogramSum());

        exportedItems.Clear();
        metricPoints.Clear();

        histogram.Record(15); // Record another measurement for zero tags

        // Emit 2500 more newer MetricPoints with distinct dimension combinations
        for (var i = 2001; i < 4501; i++)
        {
            histogram.Record(5, new KeyValuePair<string, object?>("Key", i));
        }

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);
        overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

        if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
        {
            Assert.Equal(15, zeroTagsMetricPoint.GetHistogramSum());

            int expectedCount;
            int expectedSum;

            // Number of metric points that were available before the 2500 measurements were made = 2000 (max MetricPoints)
            // Because unused metric points are reclaimed, number of metric points dropped = 2500 - 2000 = 500
            expectedCount = 500;
            expectedSum = 2500; // 500 * 5

            Assert.Equal(expectedCount, overflowMetricPoint.GetHistogramCount());
            Assert.Equal(expectedSum, overflowMetricPoint.GetHistogramSum());
        }
        else
        {
            Assert.Equal(25, zeroTagsMetricPoint.GetHistogramSum());
            Assert.Equal(2501, overflowMetricPoint.GetHistogramCount());
            Assert.Equal(12505, overflowMetricPoint.GetHistogramSum()); // 5 + (2500 * 5)
        }

        exportedItems.Clear();
        metricPoints.Clear();

        // Test that the SDK continues to correctly aggregate the previously registered measurements even after overflow has occurred
        histogram.Record(25);

        meterProvider.ForceFlush();
        metric = exportedItems[0];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        zeroTagsMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 0);

        if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
        {
            Assert.Equal(25, zeroTagsMetricPoint.GetHistogramSum());
        }
        else
        {
            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            Assert.Equal(50, zeroTagsMetricPoint.GetHistogramSum());
            Assert.Equal(12505, overflowMetricPoint.GetHistogramSum());
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForMultipleTags(MetricReaderTemporalityPreference temporalityPreference)
    {
        // Every measurement carries two tags so the multi-tag lookup path hits the limit.
        const int CardinalityLimit = 10;

        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("TestCounter", new MetricStreamConfiguration { CardinalityLimit = CardinalityLimit })
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        for (var i = 0; i < CardinalityLimit; i++)
        {
            counter.Add(10, new KeyValuePair<string, object?>("Key1", i), new KeyValuePair<string, object?>("Key2", "fixed"));
        }

        // Store is full: new tag sets in either key order go to the overflow point.
        counter.Add(5, new KeyValuePair<string, object?>("Key1", CardinalityLimit), new KeyValuePair<string, object?>("Key2", "fixed"));
        counter.Add(7, new KeyValuePair<string, object?>("Key2", "fixed"), new KeyValuePair<string, object?>("Key1", CardinalityLimit + 1));

        meterProvider.ForceFlush();

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Equal(CardinalityLimit, metricPoints.Count(mp => mp.Tags.Count == 2));

        var overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 1 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
        Assert.Equal(12, overflowMetricPoint.GetSumLong());

        exportedItems.Clear();
        metricPoints.Clear();

        // A collect with no measurements lets delta reclaim the points; cumulative never reclaims.
        meterProvider.ForceFlush();

        exportedItems.Clear();

        for (var i = 100; i < 100 + CardinalityLimit + 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("Key1", i), new KeyValuePair<string, object?>("Key2", "fixed"));
        }

        meterProvider.ForceFlush();

        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        overflowMetricPoint = metricPoints.Single(mp => mp.Tags.Count == 1 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

        if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
        {
            Assert.Equal(CardinalityLimit, metricPoints.Count(mp => mp.Tags.Count == 2));
            Assert.Equal(3, overflowMetricPoint.GetSumLong());
        }
        else
        {
            Assert.Equal(CardinalityLimit, metricPoints.Count(mp => mp.Tags.Count == 2));
            Assert.Equal(12 + CardinalityLimit + 3, overflowMetricPoint.GetSumLong());
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta, 1)]
    [InlineData(MetricReaderTemporalityPreference.Delta, 2)]
    [InlineData(MetricReaderTemporalityPreference.Delta, 7)]
    [InlineData(MetricReaderTemporalityPreference.Delta, 8)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, 1)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, 2)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, 7)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, 8)]
    public void EveryMetricPointSlotIsUsedExactlyOnceUpToTheCardinalityLimit(MetricReaderTemporalityPreference temporalityPreference, int cardinalityLimit)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("TestCounter", new MetricStreamConfiguration { CardinalityLimit = cardinalityLimit })
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        for (var i = 0; i < cardinalityLimit; i++)
        {
            counter.Add(i + 1, new KeyValuePair<string, object?>("Key", i));
        }

        // One more tag set than the limit goes to the overflow point.
        counter.Add(100, new KeyValuePair<string, object?>("Key", cardinalityLimit));

        meterProvider.ForceFlush();

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        var taggedPoints = metricPoints.Where(mp => mp.Tags.KeyAndValues[0].Key == "Key").ToList();
        Assert.Equal(cardinalityLimit, taggedPoints.Count);

        for (var i = 0; i < cardinalityLimit; i++)
        {
            var point = Assert.Single(taggedPoints, mp => Equals(mp.Tags.KeyAndValues[0].Value, i));
            Assert.Equal(i + 1, point.GetSumLong());
        }

        if (temporalityPreference == MetricReaderTemporalityPreference.Cumulative)
        {
            Assert.Equal(Enumerable.Range(0, cardinalityLimit), taggedPoints.Select(mp => (int)mp.Tags.KeyAndValues[0].Value!));
        }

        var overflowMetricPoint = Assert.Single(metricPoints, mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
        Assert.Equal(100, overflowMetricPoint.GetSumLong());
    }
}
