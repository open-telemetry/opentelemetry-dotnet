// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable SA1402

public abstract class MetricOverflowAttributeTestsBase
{
    private readonly bool shouldReclaimUnusedMetricPoints;
    private readonly Dictionary<string, string> configurationData = new()
    {
        [MetricTestsBase.EmitOverFlowAttributeConfigKey] = "true",
    };

    private readonly IConfiguration configuration;

    public MetricOverflowAttributeTestsBase(bool shouldReclaimUnusedMetricPoints)
    {
        this.shouldReclaimUnusedMetricPoints = shouldReclaimUnusedMetricPoints;

        if (shouldReclaimUnusedMetricPoints)
        {
            this.configurationData[MetricTestsBase.ReclaimUnusedMetricPointsConfigKey] = "true";
        }

        this.configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(this.configurationData)
            .Build();
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    public void TestEmitOverflowAttributeConfigWithEnvVar(string value, bool isEmitOverflowAttributeKeySet)
    {
        // Clear the environment variable value first
        Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);

        // Set the environment variable to the value provided in the test input
        Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, value);

        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        counter.Add(10);

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
        Assert.Equal(isEmitOverflowAttributeKeySet, exportedItems[0].AggregatorStore.EmitOverflowAttribute);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    public void TestEmitOverflowAttributeConfigWithOtherConfigProvider(string value, bool isEmitOverflowAttributeKeySet)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { [MetricTestsBase.EmitOverFlowAttributeConfigKey] = value })
                .Build();

                services.AddSingleton<IConfiguration>(configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        counter.Add(10);

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
        Assert.Equal(isEmitOverflowAttributeKeySet, exportedItems[0].AggregatorStore.EmitOverflowAttribute);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void EmitOverflowAttributeIsNotDependentOnMaxMetricPoints(int maxMetricPoints)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .SetMaxMetricPointsPerMetricStream(maxMetricPoints)
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        counter.Add(10);

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForCounter(MetricReaderTemporalityPreference temporalityPreference)
    {
        var exportedItems = new List<Metric>();

        var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("TestCounter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        // There are two reserved MetricPoints
        // 1. For zero tags
        // 2. For metric overflow attribute when user opts-in for this feature

        counter.Add(10); // Record measurement for zero tags

        // Max number for MetricPoints available for use when emitted with tags
        int maxMetricPointsForUse = MeterProviderBuilderSdk.DefaultCardinalityLimit;

        for (int i = 0; i < maxMetricPointsForUse; i++)
        {
            // Emit unique key-value pairs to use up the available MetricPoints
            // Once this loop is run, we have used up all available MetricPoints for metrics emitted with tags
            counter.Add(10, new KeyValuePair<string, object>("Key", i));
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

        counter.Add(5, new KeyValuePair<string, object>("Key", 2000)); // Emit a metric to exceed the max MetricPoint limit

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
        for (int i = 2001; i < 4501; i++)
        {
            counter.Add(5, new KeyValuePair<string, object>("Key", i));
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
            if (this.shouldReclaimUnusedMetricPoints)
            {
                // If unused metric points are reclaimed, then number of metric points dropped = 2500 - 2000 = 500
                expectedSum = 2500; // 500 * 5
            }
            else
            {
                expectedSum = 12500; // 2500 * 5
            }

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
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions => metricReaderOptions.TemporalityPreference = temporalityPreference)
            .Build();

        // There are two reserved MetricPoints
        // 1. For zero tags
        // 2. For metric overflow attribute when user opts-in for this feature

        histogram.Record(10); // Record measurement for zero tags

        // Max number for MetricPoints available for use when emitted with tags
        int maxMetricPointsForUse = MeterProviderBuilderSdk.DefaultCardinalityLimit;

        for (int i = 0; i < maxMetricPointsForUse; i++)
        {
            // Emit unique key-value pairs to use up the available MetricPoints
            // Once this loop is run, we have used up all available MetricPoints for metrics emitted with tags
            histogram.Record(10, new KeyValuePair<string, object>("Key", i));
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

        histogram.Record(5, new KeyValuePair<string, object>("Key", 2000)); // Emit a metric to exceed the max MetricPoint limit

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
        for (int i = 2002; i < 4502; i++)
        {
            histogram.Record(5, new KeyValuePair<string, object>("Key", i));
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
            if (this.shouldReclaimUnusedMetricPoints)
            {
                // If unused metric points are reclaimed, then number of metric points dropped = 2500 - 2000 = 500
                expectedCount = 500;
                expectedSum = 2500; // 500 * 5
            }
            else
            {
                expectedCount = 2500;
                expectedSum = 12500; // 2500 * 5
            }

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
}

public class MetricOverflowAttributeTests : MetricOverflowAttributeTestsBase
{
    public MetricOverflowAttributeTests()
        : base(false)
    {
    }
}

public class MetricOverflowAttributeTestsWithReclaimAttribute : MetricOverflowAttributeTestsBase
{
    public MetricOverflowAttributeTestsWithReclaimAttribute()
        : base(true)
    {
    }
}
