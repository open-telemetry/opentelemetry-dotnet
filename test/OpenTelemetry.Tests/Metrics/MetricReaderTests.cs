// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricReaderTests
{
    [Theory]
    [InlineData("counter", typeof(long), AggregationTemporality.Delta)]
    [InlineData("counter", typeof(double), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(long), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(double), AggregationTemporality.Delta)]
    [InlineData("updowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("updowncounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(double), AggregationTemporality.Cumulative)]
    public void LowMemoryTemporality_UsesCorrectAggregationTemporality(string instrumentName, Type valueType, AggregationTemporality expectedTemporality)
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
                if (valueType == typeof(long))
                {
                    meter.CreateCounter<long>("test_counter").Add(1);
                }
                else
                {
                    meter.CreateCounter<double>("test_counter").Add(1);
                }

                break;

            case "histogram":
                if (valueType == typeof(long))
                {
                    meter.CreateHistogram<long>("test_histogram").Record(1);
                }
                else
                {
                    meter.CreateHistogram<double>("test_histogram").Record(1);
                }

                break;

            case "updowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateUpDownCounter<long>("test_updown").Add(1);
                }
                else
                {
                    meter.CreateUpDownCounter<double>("test_updown").Add(1);
                }

                break;

            case "observablecounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<double>(1));
                }

                break;

            case "observableupdowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<double>(1));
                }

                break;
        }

        provider.ForceFlush();

        var metric = Assert.Single(metrics);
        Assert.Equal(expectedTemporality, metric.Temporality);
    }
}
