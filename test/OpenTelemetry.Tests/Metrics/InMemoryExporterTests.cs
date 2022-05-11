// <copyright file="InMemoryExporterTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;

using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class InMemoryExporterTests
    {
        [Fact]
        public void InMemoryExporterShouldDeepCopyMetricPoints()
        {
            var exportedItems = new List<ReadOnlyMetric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                })
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            var metric = exportedItems[0]; // Only one Metric object is added to the collection at this point
            var metricPointsEnumerator = metric.MetricPoints.GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            var metricPointForFirstExport = metricPointsEnumerator.Current;
            Assert.Equal(10, metricPointForFirstExport.GetSumLong());

            // Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            metric = exportedItems[1]; // Second Metric object is added to the collection at this point
            metricPointsEnumerator = metric.MetricPoints.GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            var metricPointForSecondExport = metricPointsEnumerator.Current;
            Assert.Equal(25, metricPointForSecondExport.GetSumLong());

            // MetricPoint.LongValue for the first exporter metric should still be 10
            Assert.Equal(10, metricPointForFirstExport.GetSumLong());
        }
    }
}
