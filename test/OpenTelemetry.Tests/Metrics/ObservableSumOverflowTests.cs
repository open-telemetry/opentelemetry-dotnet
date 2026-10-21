// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class ObservableSumOverflowTests
{
    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta, false)]
    [InlineData(MetricReaderTemporalityPreference.Delta, true)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, false)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, true)]
    public void OverflowSumsAllObservationsInEachCollection(
        MetricReaderTemporalityPreference temporality,
        bool upDownCounter)
    {
        var exportedItems = new List<Metric>();
        var values = new List<long> { 100, 200, 300, 400, 500, 600 };
        var reverse = false;
        var sign = upDownCounter ? -1 : 1;

        using var meter = new Meter(new MeterOptions(Utils.GetCurrentMethodName()));
        IEnumerable<Measurement<long>> ObserveLong()
        {
            var measurements = values.Select((value, index) =>
                new Measurement<long>(sign * value, new KeyValuePair<string, object?>("Key", index)));
            return reverse ? measurements.Reverse() : measurements;
        }

        IEnumerable<Measurement<double>> ObserveDouble() => ObserveLong().Select(measurement =>
            new Measurement<double>(measurement.Value / 2.0, measurement.Tags.ToArray()));

        if (upDownCounter)
        {
            meter.CreateObservableUpDownCounter("Long", ObserveLong);
            meter.CreateObservableUpDownCounter("Double", ObserveDouble);
        }
        else
        {
            meter.CreateObservableCounter("Long", ObserveLong);
            meter.CreateObservableCounter("Double", ObserveDouble);
        }

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("*", new MetricStreamConfiguration { CardinalityLimit = 2 })
            .AddInMemoryExporter(exportedItems, options => options.TemporalityPreference = temporality)
            .Build();

        Assert.True(provider.ForceFlush());
        AssertSums(exportedItems, sign * 100, sign * 200, sign * 1800);

        // The series keep their identities when observation order changes.
        values.Add(5);
        reverse = true;
        exportedItems.Clear();
        Assert.True(provider.ForceFlush());

        // Readers always use cumulative temporality for UpDownCounter instruments.
        var delta = temporality == MetricReaderTemporalityPreference.Delta && !upDownCounter;
        AssertSums(exportedItems, delta ? 0 : sign * 100, delta ? 0 : sign * 200, sign * (delta ? 5 : 1805));

        // Repeating the same observations must not accumulate another cycle's totals.
        exportedItems.Clear();
        Assert.True(provider.ForceFlush());
        AssertSums(exportedItems, delta ? 0 : sign * 100, delta ? 0 : sign * 200, delta ? 0 : sign * 1805);
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
    public void OverflowSumsAreIndependentOfOtherReaders(
        MetricReaderTemporalityPreference firstTemporality,
        MetricReaderTemporalityPreference secondTemporality)
    {
        var firstItems = new List<Metric>();
        var secondItems = new List<Metric>();
        var values = new List<long> { 100, 200, 300, 400, 500, 600 };

        using var meter = new Meter(new MeterOptions(Utils.GetCurrentMethodName()));
        meter.CreateObservableCounter("Long", () => values.Select((value, index) =>
            new Measurement<long>(value, new KeyValuePair<string, object?>("Key", index))));
        meter.CreateObservableCounter("Double", () => values.Select((value, index) =>
            new Measurement<double>(value / 2.0, new KeyValuePair<string, object?>("Key", index))));

        using var firstExporter = new InMemoryExporter<Metric>(firstItems);
        using var secondExporter = new InMemoryExporter<Metric>(secondItems);
        using var firstReader = new BaseExportingMetricReader(firstExporter)
        {
            TemporalityPreference = firstTemporality,
        };
        using var secondReader = new BaseExportingMetricReader(secondExporter)
        {
            TemporalityPreference = secondTemporality,
        };
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("*", new MetricStreamConfiguration { CardinalityLimit = 2 })
            .AddReader(firstReader)
            .AddReader(secondReader)
            .Build();

        // Each collection observes into both readers, but only snapshots the collecting reader.
        Assert.True(firstReader.Collect());
        firstItems.Clear();
        Assert.True(firstReader.Collect());
        Assert.True(secondReader.Collect());
        AssertSums(secondItems, 100, 200, 1800);

        values.Add(5);
        firstItems.Clear();
        secondItems.Clear();
        Assert.True(provider.ForceFlush());
        var firstDelta = firstTemporality == MetricReaderTemporalityPreference.Delta;
        var secondDelta = secondTemporality == MetricReaderTemporalityPreference.Delta;
        AssertSums(firstItems, firstDelta ? 0 : 100, firstDelta ? 0 : 200, firstDelta ? 5 : 1805);
        AssertSums(secondItems, secondDelta ? 0 : 100, secondDelta ? 0 : 200, secondDelta ? 5 : 1805);
    }

    [Fact]
    public void OverflowPreservesGaugeAndNonOverflowLastValueSemantics()
    {
        var exportedItems = new List<Metric>();
        var observations = new Measurement<long>[]
        {
            new(3, new KeyValuePair<string, object?>("Key", 0)),
            new(9, new KeyValuePair<string, object?>("Key", 0)),
            new(100, new KeyValuePair<string, object?>("Key", 1)),
            new(300, new KeyValuePair<string, object?>("Key", 2)),
            new(400, new KeyValuePair<string, object?>("Key", 3)),
        };

        using var meter = new Meter(new MeterOptions(Utils.GetCurrentMethodName()));
        meter.CreateObservableCounter("Long", () => observations);
        meter.CreateObservableCounter("Double", () => observations.Select(measurement =>
            new Measurement<double>(measurement.Value / 2.0, measurement.Tags.ToArray())));
        meter.CreateObservableGauge("LongGauge", () => observations);
        meter.CreateObservableGauge("DoubleGauge", () => observations.Select(measurement =>
            new Measurement<double>(measurement.Value / 2.0, measurement.Tags.ToArray())));
        meter.CreateObservableCounter("Filtered", () => observations);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("*", new MetricStreamConfiguration { CardinalityLimit = 2 })
            .AddView("Filtered", new MetricStreamConfiguration { Name = "NoTags", TagKeys = [] })
            .AddInMemoryExporter(exportedItems)
            .Build();

        Assert.True(provider.ForceFlush());
        AssertSums(exportedItems.Where(metric => metric.Name is "Long" or "Double").ToList(), 9, 100, 700);
        Assert.Equal(400, GetOverflowPoint(exportedItems, "LongGauge").GetGaugeLastValueLong());
        Assert.Equal(200, GetOverflowPoint(exportedItems, "DoubleGauge").GetGaugeLastValueDouble());
        var filteredPoints = GetPoints(exportedItems.Single(metric => metric.Name == "NoTags"));
        Assert.Equal(400, Assert.Single(filteredPoints).GetSumLong());
    }

    [Fact]
    public void OverflowSumsSurviveCollectionOfAnotherProviderInTheCallback()
    {
        var outerItems = new List<Metric>();
        var nestedItems = new List<Metric>();
        using var nestedMeter = new Meter(new MeterOptions($"{Utils.GetCurrentMethodName()}.Nested"));
        nestedMeter.CreateObservableCounter("Long", () => Enumerable.Range(0, 6).Select(index =>
            new Measurement<long>((index + 1) * 100, new KeyValuePair<string, object?>("Key", index))));
        nestedMeter.CreateObservableCounter("Double", () => Enumerable.Range(0, 6).Select(index =>
            new Measurement<double>((index + 1) * 50, new KeyValuePair<string, object?>("Key", index))));
        using var nestedProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(nestedMeter.Name)
            .AddView("*", new MetricStreamConfiguration { CardinalityLimit = 2 })
            .AddInMemoryExporter(nestedItems)
            .Build();

        IEnumerable<Measurement<long>> ObserveOuter()
        {
            for (var index = 0; index < 6; index++)
            {
                yield return new((index + 1) * 100, new KeyValuePair<string, object?>("Key", index));
                if (index == 2)
                {
                    nestedItems.Clear();
                    Assert.True(nestedProvider.ForceFlush());
                }
            }
        }

        using var outerMeter = new Meter(new MeterOptions(Utils.GetCurrentMethodName()));
        outerMeter.CreateObservableCounter("Long", ObserveOuter);
        outerMeter.CreateObservableCounter("Double", () => ObserveOuter().Select(measurement =>
            new Measurement<double>(measurement.Value / 2.0, measurement.Tags.ToArray())));
        using var outerProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(outerMeter.Name)
            .AddView("*", new MetricStreamConfiguration { CardinalityLimit = 2 })
            .AddInMemoryExporter(outerItems)
            .Build();

        Assert.True(outerProvider.ForceFlush());
        AssertSums(outerItems, 100, 200, 1800);
        AssertSums(nestedItems, 100, 200, 1800);
    }

    private static void AssertSums(List<Metric> metrics, long firstValue, long secondValue, long overflowValue)
    {
        Assert.Equal(2, metrics.Count);
        foreach (var metric in metrics)
        {
            var points = GetPoints(metric);
            Assert.Equal(3, points.Count);
            var first = Assert.Single(points, point => Equals(point.Tags.KeyAndValues[0].Value, 0));
            var second = Assert.Single(points, point => Equals(point.Tags.KeyAndValues[0].Value, 1));
            var overflow = GetOverflowPoint(metrics, metric.Name);
            if (metric.Name == "Long")
            {
                Assert.Equal(firstValue, first.GetSumLong());
                Assert.Equal(secondValue, second.GetSumLong());
                Assert.Equal(overflowValue, overflow.GetSumLong());
            }
            else
            {
                Assert.Equal("Double", metric.Name);
                Assert.Equal(firstValue / 2.0, first.GetSumDouble());
                Assert.Equal(secondValue / 2.0, second.GetSumDouble());
                Assert.Equal(overflowValue / 2.0, overflow.GetSumDouble());
            }
        }
    }

    private static MetricPoint GetOverflowPoint(List<Metric> metrics, string name)
    {
        var point = Assert.Single(
            GetPoints(metrics.Single(metric => metric.Name == name)),
            point => point.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
        Assert.Single(point.Tags.KeyAndValues);
        Assert.Equal(true, point.Tags.KeyAndValues[0].Value);
        return point;
    }

    private static List<MetricPoint> GetPoints(Metric metric)
    {
        var points = new List<MetricPoint>();
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            points.Add(point);
        }

        return points;
    }
}
