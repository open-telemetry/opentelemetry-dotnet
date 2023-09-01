// <copyright file="MetricOverflowAttributeTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

            counter.Add(10); // Record measurement for zero tags

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
            Assert.DoesNotContain(metricPoints, mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            exportedItems.Clear();
            metricPoints.Clear();

            counter.Add(5, new KeyValuePair<string, object>("Key", 1998)); // Emit a metric to exceed the max MetricPoint limit

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
            for (int i = 2000; i < 4500; i++)
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

                // Number of metric points that were available before the 2500 measurements were made = 2000 (max MetricPoints) - 2 (reserved for zero tags and overflow) = 1998
                // Number of metric points dropped = 2500 - 1998 = 502
                Assert.Equal(2510, overflowMetricPoint.GetSumLong()); // 502 * 5
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

            histogram.Record(10); // Record measurement for zero tags

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
            Assert.DoesNotContain(metricPoints, mp => mp.Tags.Count != 0 && mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");

            exportedItems.Clear();
            metricPoints.Clear();

            histogram.Record(5, new KeyValuePair<string, object>("Key", 1998)); // Emit a metric to exceed the max MetricPoint limit

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
            for (int i = 2000; i < 4500; i++)
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

                // Number of metric points that were available before the 2500 measurements were made = 2000 (max MetricPoints) - 2 (reserved for zero tags and overflow) = 1998
                // Number of metric points dropped = 2500 - 1998 = 502
                Assert.Equal(502, overflowMetricPoint.GetHistogramCount());
                Assert.Equal(2510, overflowMetricPoint.GetHistogramSum()); // 502 * 5
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
        finally
        {
            Environment.SetEnvironmentVariable(MetricTestsBase.EmitOverFlowAttributeConfigKey, null);
        }
    }
}
