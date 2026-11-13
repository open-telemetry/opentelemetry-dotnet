// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class DoubleDeltaAggregationTests
{
    [Theory]
    [InlineData(double.NaN, false)]
    [InlineData(double.NaN, true)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(1e19, false)]
    [InlineData(1e19, true)]
    [InlineData(1e17, false)]
    [InlineData(1e17, true)]
    public void CounterDeltaOnlyContainsMeasurementsFromTheCurrentInterval(double measurement, bool tagged)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counterDouble = meter.CreateCounter<double>("double-counter");
        var counterFloat = meter.CreateCounter<float>("float-counter");
        var snapshots = new List<MetricSnapshot>();
        var tags = tagged ? new KeyValuePair<string, object?>[] { new("key", "value") } : [];

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(snapshots, options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
            .Build();

        counterDouble.Add(measurement, tags);
        counterFloat.Add((float)measurement, tags);
        Assert.True(meterProvider.ForceFlush());
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(measurement, Assert.Single(snapshots[0].MetricPoints).GetSumDouble());
        Assert.Equal((double)(float)measurement, Assert.Single(snapshots[1].MetricPoints).GetSumDouble());

        for (var interval = 0; interval < 3; interval++)
        {
            snapshots.Clear();
            for (var i = 0; i < 100; i++)
            {
                counterDouble.Add(10, tags);
                counterFloat.Add(10, tags);
            }

            Assert.True(meterProvider.ForceFlush());
            Assert.Equal(2, snapshots.Count);
            Assert.All(snapshots, snapshot => Assert.Equal(1000, Assert.Single(snapshot.MetricPoints).GetSumDouble()));
        }

        snapshots.Clear();
        Assert.True(meterProvider.ForceFlush());
        Assert.Empty(snapshots);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void IdleNaNPointIsReclaimed(bool observable, bool gauge)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var snapshots = new List<MetricSnapshot>();
        var observations = new List<Measurement<double>>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("metric", new MetricStreamConfiguration { CardinalityLimit = 1 })
            .AddInMemoryExporter(snapshots, options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
            .Build();

        Action<double, int> record;
        if (observable)
        {
            if (gauge)
            {
                meter.CreateObservableGauge("metric", () => observations);
            }
            else
            {
                meter.CreateObservableCounter("metric", () => observations);
            }

            record = (value, tag) => observations.Add(new(value, new KeyValuePair<string, object?>("key", tag)));
        }
        else if (gauge)
        {
            var instrument = meter.CreateGauge<double>("metric");
            record = (value, tag) => instrument.Record(value, new KeyValuePair<string, object?>("key", tag));
        }
        else
        {
            var instrument = meter.CreateCounter<double>("metric");
            record = (value, tag) => instrument.Add(value, new KeyValuePair<string, object?>("key", tag));
        }

        record(double.NaN, 0);
        Assert.True(meterProvider.ForceFlush());
        AssertPoint(double.NaN, 0);
        observations.Clear();
        snapshots.Clear();

        Assert.True(meterProvider.ForceFlush());
        Assert.Empty(snapshots);

        record(1, 1);
        Assert.True(meterProvider.ForceFlush());
        AssertPoint(1, 1);

        void AssertPoint(double expectedValue, int expectedTag)
        {
            var point = Assert.Single(Assert.Single(snapshots).MetricPoints);
            Assert.Equal(new KeyValuePair<string, object?>("key", expectedTag), Assert.Single(point.Tags.KeyAndValues));
            Assert.Equal(expectedValue, gauge ? point.GetGaugeLastValueDouble() : point.GetSumDouble());
        }
    }
}
