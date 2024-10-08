// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MemoryEfficiencyTests
{
    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void ExportOnlyWhenPointChanged(MetricReaderTemporalityPreference temporality)
    {
        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");

        var exportedItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            })
            .Build();

        var counter = meter.CreateCounter<long>("meter");

        counter.Add(10, new KeyValuePair<string, object?>("tag1", "value1"));
        meterProvider.ForceFlush();
        Assert.Single(exportedItems);

        exportedItems.Clear();
        meterProvider.ForceFlush();
        if (temporality == MetricReaderTemporalityPreference.Cumulative)
        {
            Assert.Single(exportedItems);
        }
        else
        {
            Assert.Empty(exportedItems);
        }
    }
}
