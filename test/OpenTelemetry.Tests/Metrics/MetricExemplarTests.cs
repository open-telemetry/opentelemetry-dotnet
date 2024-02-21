// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricExemplarTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsCounter(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var counter = meter.CreateCounter<double>("testCounter");

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddView(
                "testCounter",
                new MetricStreamConfiguration
                {
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = GenerateRandomValues(2, false, null);
        foreach (var value in measurementValues)
        {
            counter.Add(value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        var exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = GenerateRandomValues(1, true, measurementValues);
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            counter.Add(value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        exemplars = GetExemplars(metricPoint.Value);

        if (temporality == MetricReaderTemporalityPreference.Cumulative)
        {
            Assert.Equal(3, exemplars.Count);
            secondMeasurementValues = secondMeasurementValues.Concat(measurementValues).ToArray();
        }
        else
        {
            Assert.Single(exemplars);
        }

        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues);
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsHistogram(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var histogram = meter.CreateHistogram<double>("testHistogram");

        var buckets = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddView(
                "testHistogram",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = buckets,
                })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = buckets
            /* 2000 is here to test overflow measurement */
            .Concat(new double[] { 2000 })
            .Select(b => (Value: b, ExpectTraceId: false))
            .ToArray();
        foreach (var value in measurementValues)
        {
            histogram.Record(value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = buckets.Take(1).Select(b => (Value: b, ExpectTraceId: true)).ToArray();
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            histogram.Record(value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        exemplars = GetExemplars(metricPoint.Value);

        if (temporality == MetricReaderTemporalityPreference.Cumulative)
        {
            Assert.Equal(11, exemplars.Count);
            secondMeasurementValues = secondMeasurementValues.Concat(measurementValues).ToArray();
        }
        else
        {
            Assert.Single(exemplars);
        }

        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues);
    }

    [Fact]
    public void TestExemplarsFilterTags()
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var histogram = meter.CreateHistogram<double>("testHistogram");

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddView(histogram.Name, new MetricStreamConfiguration() { TagKeys = new string[] { "key1" } })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            }));

        var measurementValues = GenerateRandomValues(10, false, null);
        foreach (var value in measurementValues)
        {
            histogram.Record(
                value.Value,
                new("key1", "value1"),
                new("key2", "value1"),
                new("key3", "value1"));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        foreach (var exemplar in exemplars)
        {
            Assert.NotEqual(0, exemplar.FilteredTags.MaximumCount);

            var filteredTags = exemplar.FilteredTags.ToReadOnlyList();

            Assert.Contains(new("key2", "value1"), filteredTags);
            Assert.Contains(new("key3", "value1"), filteredTags);
        }
    }

    private static (double Value, bool ExpectTraceId)[] GenerateRandomValues(
        int count,
        bool expectTraceId,
        (double Value, bool ExpectTraceId)[]? previousValues)
    {
        var random = new Random();
        var values = new (double, bool)[count];
        for (int i = 0; i < count; i++)
        {
            var nextValue = random.NextDouble();
            if (values.Any(m => m.Item1 == nextValue)
                || previousValues?.Any(m => m.Value == nextValue) == true)
            {
                i--;
                continue;
            }

            values[i] = (nextValue, expectTraceId);
        }

        return values;
    }

    private static void ValidateExemplars(
        IReadOnlyList<Exemplar> exemplars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        (double Value, bool ExpectTraceId)[] measurementValues)
    {
        foreach (var exemplar in exemplars)
        {
            Assert.True(exemplar.Timestamp >= startTime && exemplar.Timestamp <= endTime, $"{startTime} < {exemplar.Timestamp} < {endTime}");
            Assert.Equal(0, exemplar.FilteredTags.MaximumCount);

            var measurement = measurementValues.FirstOrDefault(v => v.Value == exemplar.DoubleValue);
            Assert.NotEqual(default, measurement);
            if (measurement.ExpectTraceId)
            {
                Assert.NotEqual(default, exemplar.TraceId);
                Assert.NotEqual(default, exemplar.SpanId);
            }
            else
            {
                Assert.Equal(default, exemplar.TraceId);
                Assert.Equal(default, exemplar.SpanId);
            }
        }
    }
}
