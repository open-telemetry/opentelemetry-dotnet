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
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricOverflowAttributeTests
{
    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    public void MetricOverflowAttributeIsRecordedCorrectly(MetricReaderTemporalityPreference temporalityPreference)
    {
        try
        {
            AppContext.SetSwitch("OTel.Dotnet.EmitMetricOverflowAttribute", true);

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
            if (temporalityPreference == MetricReaderTemporalityPreference.Delta)
            {
                Assert.DoesNotContain(metricPoints, mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
            }
            else
            {
                overflowMetricPoint = metricPoints.Single(mp => mp.Tags.KeyAndValues[0].Key == "otel.metric.overflow");
                Assert.Equal(true, overflowMetricPoint.Tags.KeyAndValues[0].Value);
                Assert.Equal(0, overflowMetricPoint.GetSumLong()); // No recording should have been made for the overflow attribute at this point
            }

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
        }
        finally
        {
            AppContext.SetSwitch("OTel.Dotnet.EmitMetricOverflowAttribute", false);
        }
    }
}
