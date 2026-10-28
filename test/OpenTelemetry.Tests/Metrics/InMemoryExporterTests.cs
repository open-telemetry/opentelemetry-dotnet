// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class InMemoryExporterTests
{
    [Fact]
    public void InMemoryExporterShouldDeepCopyMetricPoints()
    {
        var exportedItems = new List<MetricSnapshot>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            })
            .Build();

        var counter = meter.CreateCounter<long>("meter");

        // TEST 1: Emit 10 for the MetricPoint with a single key-value pair: ("tag1", "value1")
        counter.Add(10, new KeyValuePair<string, object?>("tag1", "value1"));

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
        var metric1 = exportedItems[0]; // Only one Metric object is added to the collection at this point
        Assert.Single(metric1.MetricPoints);
        Assert.Equal(10, metric1.MetricPoints[0].GetSumLong());

        // TEST 2: Emit 25 for the MetricPoint with a single key-value pair: ("tag1", "value1")
        counter.Add(25, new KeyValuePair<string, object?>("tag1", "value1"));

        meterProvider.ForceFlush();

        Assert.Equal(2, exportedItems.Count);
        var metric2 = exportedItems[1]; // Second Metric object is added to the collection at this point
        Assert.Single(metric2.MetricPoints);
        Assert.Equal(25, metric2.MetricPoints[0].GetSumLong());

        // TEST 3: Verify first exported metric is unchanged
        // MetricPoint.LongValue for the first exported metric should still be 10
        Assert.Equal(10, metric1.MetricPoints[0].GetSumLong());
    }

    [Fact]
    public void InMemoryExporterShouldNotDuplicateMetricPointsSplitBetweenBatches()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.MaxExportBatchSize = 1;
            })
            .Build();

        var counter = meter.CreateCounter<long>("meter");

        for (var i = 0; i < 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("key", i));
        }

        meterProvider.ForceFlush();

        var metric = Assert.Single(exportedItems);

        var count = 0;

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            count++;
        }

        Assert.Equal(3, count);

        // The metric is added again for the next collection
        counter.Add(1, new KeyValuePair<string, object?>("key", 0));

        meterProvider.ForceFlush();

        Assert.Equal(2, exportedItems.Count);
    }
}
