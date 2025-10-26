// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricReaderTests
{
    [Theory]
    [InlineData("counter", AggregationTemporality.Delta)]
    [InlineData("histogram", AggregationTemporality.Delta)]
    [InlineData("updowncounter", AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", AggregationTemporality.Cumulative)]
    public void LowMemoryTemporality_UsesCorrectAggregationTemporality(string instrumentName, AggregationTemporality expectedTemporality)
    {
        var metrics = new List<Metric>();
        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.LowMemory;
            })
            .Build();

        switch (instrumentName)
        {
            case "counter":
                var counter = meter.CreateCounter<long>("test_counter");
                counter.Add(1);
                break;

            case "histogram":
                var histogram = meter.CreateHistogram<long>("test_histogram");
                histogram.Record(1);
                break;

            case "updowncounter":
                var upDown = meter.CreateUpDownCounter<long>("test_updown");
                upDown.Add(1);
                break;

            case "observablecounter":
                meter.CreateObservableCounter("test_observable_counter", () => new Measurement<long>(1));
                break;

            case "observableupdowncounter":
                meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<long>(1));
                break;
        }

        provider.ForceFlush();

        var metric = Assert.Single(metrics);
        Assert.Equal(expectedTemporality, metric.Temporality);
    }
}
