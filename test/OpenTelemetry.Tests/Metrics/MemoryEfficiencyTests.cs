// <copyright file="MemoryEfficiencyTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Tests
{
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

            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));
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

        [Theory]
        [InlineData(MetricReaderTemporalityPreference.Cumulative)]
        [InlineData(MetricReaderTemporalityPreference.Delta)]
        public void ObservableInstrument_ReclaimDataPointsWhenUnobserved(MetricReaderTemporalityPreference temporality)
        {
            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");

            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetMaxMetricPointsPerMetricStream(3) // metricPoints[0] is always reserved for tagless metric, so set 3 points max.
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems, (metricReaderOptions) =>
                {
                    metricReaderOptions.TemporalityPreference = temporality;
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)TimeSpan.FromDays(5).TotalMilliseconds; // manual
                })
                .Build();

            var measurements = new List<Measurement<long>>()
            {
                new(1, new KeyValuePair<string, object>[] { new("name", "a"), new("n", 1) }),
                new(2, new KeyValuePair<string, object>[] { new("name", "b"), new("n", 2) }),
                new(3, new KeyValuePair<string, object>[] { new("name", "c"), new("n", 3) }),
            };

            var counter = meter.CreateObservableGauge("meter", () => measurements);

            void ValidateMetricDataPoints(Metric metric)
            {
                int i = 0;
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    var measurement = measurements[i++];

                    var tags = new Dictionary<string, object>();
                    foreach (var tag in metricPoint.Tags)
                    {
                        tags[tag.Key] = tag.Value;
                    }

                    var expectedTags = new Dictionary<string, object>();
                    foreach (var tag in measurement.Tags)
                    {
                        expectedTags[tag.Key] = tag.Value;
                    }

                    // Verify value.
                    Assert.Equal(measurement.Value, metricPoint.GetGaugeLastValueLong());

                    // Verify tags.
                    foreach (var kv in tags)
                    {
                        Assert.Equal(expectedTags[kv.Key], kv.Value);
                    }
                }

                Assert.Equal(2, i);
            }

            // First collect has [1, 2]. 3 is not exported because no data points are available.
            meterProvider.ForceFlush();
            Assert.Collection(exportedItems, ValidateMetricDataPoints);

            exportedItems.Clear();
            measurements.RemoveAt(1); // [1, 3]

            // Second collect has [1, 3]. 2 is no longer exported and 3 re-claims its data point.
            meterProvider.ForceFlush();
            Assert.Collection(exportedItems, ValidateMetricDataPoints);

            measurements.Clear();
            exportedItems.Clear();

            // Last collect has []. No measurements observed, everything is reclaimed.
            meterProvider.ForceFlush();
            Assert.Empty(exportedItems);
        }
    }
}
