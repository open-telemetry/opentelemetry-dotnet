// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

public class MetricExemplarTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;
    private readonly ITestOutputHelper output;

    public MetricExemplarTests(ITestOutputHelper output)
    {
        this.output = output;
    }

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
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            counter.Add(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);

        // TODO: Modify the test to better test cumulative.
        // In cumulative where SimpleExemplarReservoir's size is
        // more than the count of new measurements, it is possible
        // that the exemplar value is for a measurement that was recorded in the prior
        // cycle. The current ValidateExemplars() does not handle this case.
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, false);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            var act = new Activity("test").Start();
            counter.Add(value);
            act.Stop();
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, true);
    }

    [Fact]
    public void TestExemplarsHistogram()
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var histogram = meter.CreateHistogram<double>("testHistogram");

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            }));

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            histogram.Record(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, false);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            using var act = new Activity("test").Start();
            histogram.Record(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, true);
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

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            histogram.Record(value, new("key1", "value1"), new("key2", "value1"), new("key3", "value1"));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        Assert.NotNull(exemplars);
        foreach (var exemplar in exemplars)
        {
            Assert.NotNull(exemplar.FilteredTags);
            Assert.Contains(new("key2", "value1"), exemplar.FilteredTags);
            Assert.Contains(new("key3", "value1"), exemplar.FilteredTags);
        }
    }

    private static double[] GenerateRandomValues(int count)
    {
        var random = new Random();
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = random.NextDouble();
        }

        return values;
    }

    private static void ValidateExemplars(Exemplar[] exemplars, DateTimeOffset startTime, DateTimeOffset endTime, double[] measurementValues, bool traceContextExists)
    {
        Assert.NotNull(exemplars);
        foreach (var exemplar in exemplars)
        {
            Assert.True(exemplar.Timestamp >= startTime && exemplar.Timestamp <= endTime, $"{startTime} < {exemplar.Timestamp} < {endTime}");
            Assert.Contains(exemplar.DoubleValue, measurementValues);
            Assert.Null(exemplar.FilteredTags);
            if (traceContextExists)
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
