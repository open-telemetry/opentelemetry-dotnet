// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricOverflowAttributeTests
{
    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    public void TestEmitOverflowAttributeConfigWithEnvVar(string value, bool isEmitOverflowAttributeKeySet)
    {
        try
        {
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
            var metric = exportedItems[0];

            var aggregatorStore = typeof(Metric).GetField("aggStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(metric) as AggregatorStore;
            var emitOverflowAttribute = (bool)typeof(AggregatorStore).GetField("emitOverflowAttribute", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aggregatorStore);

            Assert.Equal(isEmitOverflowAttributeKeySet, emitOverflowAttribute);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
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
        try
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
            var metric = exportedItems[0];

            var aggregatorStore = typeof(Metric).GetField("aggStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(metric) as AggregatorStore;
            var emitOverflowAttribute = (bool)typeof(AggregatorStore).GetField("emitOverflowAttribute", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aggregatorStore);

            Assert.Equal(isEmitOverflowAttributeKeySet, emitOverflowAttribute);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    public void EmitOverflowAttributeIsOnlySetWhenMaxMetricPointsIsGreaterThanOne(int maxMetricPoints, bool isEmitOverflowAttributeKeySet)
    {
        try
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, "true");

            var exportedItems = new List<Metric>();

            var meter = new Meter(Utils.GetCurrentMethodName());
            var counter = meter.CreateCounter<long>("TestCounter");

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetMaxMetricPointsPerMetricStream(maxMetricPoints)
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            counter.Add(10);

            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
            var metric = exportedItems[0];

            var aggregatorStore = typeof(Metric).GetField("aggStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(metric) as AggregatorStore;
            var emitOverflowAttribute = (bool)typeof(AggregatorStore).GetField("emitOverflowAttribute", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aggregatorStore);

            Assert.Equal(isEmitOverflowAttributeKeySet, emitOverflowAttribute);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForCounter(MetricReaderTemporalityPreference temporalityPreference)
    {
        try
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, "true");

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

            // Max number for MetricPoints available for use when emitted with tags
            int maxMetricPointsForUse = MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault - 2;

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
            Assert.DoesNotContain(metricPoints, mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            exportedItems.Clear();
            metricPoints.Clear();

            counter.Add(5, new KeyValuePair<string, object>("Key", 9999)); // Emit a metric to exceed the max MetricPoint limit

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
            Assert.Equal(true, overflowMetricPoint.Tags.KeyAndValues[0].Value);
            Assert.Equal(1, overflowMetricPoint.Tags.Count);
            Assert.Equal(5, overflowMetricPoint.GetSumLong());

            exportedItems.Clear();
            metricPoints.Clear();

            // Emit 50 more newer MetricPoints with distinct dimension combinations
            for (int i = 10000; i < 10050; i++)
            {
                counter.Add(5, new KeyValuePair<string, object>("Key", i));
            }

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
            if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
            {
                Assert.Equal(250, overflowMetricPoint.GetSumLong()); // 50 * 5
            }
            else
            {
                Assert.Equal(255, overflowMetricPoint.GetSumLong()); // 5 + (50 * 5)
            }

            exportedItems.Clear();
            metricPoints.Clear();

            // Test that the SDK continues to correctly aggregate the previously registered measurements even after overflow has occurred
            counter.Add(15, new KeyValuePair<string, object>("Key", 0));

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            var metricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "Key" && (int)mp.Tags.KeyAndValues[0].Value == 0);

            if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
            {
                Assert.Equal(15, metricPoint.GetSumLong());
            }
            else
            {
                overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

                Assert.Equal(25, metricPoint.GetSumLong()); // 10 + 15
                Assert.Equal(255, overflowMetricPoint.GetSumLong());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectlyForHistogram(MetricReaderTemporalityPreference temporalityPreference)
    {
        try
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, "true");

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

            // Max number for MetricPoints available for use when emitted with tags
            int maxMetricPointsForUse = MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault - 2;

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
            Assert.DoesNotContain(metricPoints, mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            exportedItems.Clear();
            metricPoints.Clear();

            histogram.Record(5, new KeyValuePair<string, object>("Key", 9999)); // Emit a metric to exceed the max MetricPoint limit

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
            Assert.Equal(true, overflowMetricPoint.Tags.KeyAndValues[0].Value);
            Assert.Equal(1, overflowMetricPoint.GetHistogramCount());
            Assert.Equal(5, overflowMetricPoint.GetHistogramSum());

            exportedItems.Clear();
            metricPoints.Clear();

            // Emit 50 more newer MetricPoints with distinct dimension combinations
            for (int i = 10000; i < 10050; i++)
            {
                histogram.Record(5, new KeyValuePair<string, object>("Key", i));
            }

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
            if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
            {
                Assert.Equal(50, overflowMetricPoint.GetHistogramCount());
                Assert.Equal(250, overflowMetricPoint.GetHistogramSum()); // 50 * 5
            }
            else
            {
                Assert.Equal(51, overflowMetricPoint.GetHistogramCount());
                Assert.Equal(255, overflowMetricPoint.GetHistogramSum()); // 5 + (50 * 5)
            }

            exportedItems.Clear();
            metricPoints.Clear();

            // Test that the SDK continues to correctly aggregate the previously registered measurements even after overflow has occurred
            histogram.Record(15, new KeyValuePair<string, object>("Key", 0));

            meterProvider.ForceFlush();
            metric = exportedItems[0];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            var metricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "Key" && (int)mp.Tags.KeyAndValues[0].Value == 0);

            if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
            {
                Assert.Equal(1, metricPoint.GetHistogramCount());
                Assert.Equal(15, metricPoint.GetHistogramSum());
            }
            else
            {
                overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

                Assert.Equal(2, metricPoint.GetHistogramCount());
                Assert.Equal(25, metricPoint.GetHistogramSum()); // 10 + 15

                Assert.Equal(255, overflowMetricPoint.GetHistogramSum());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
    }
}
