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

using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class InMemoryExporterTests
    {
        [Fact]
        public void InMemoryExporterShouldDeepCopyMetricPoints()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = AggregationTemporality.Delta,
                })
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            var metric = exportedItems[0]; // Only one Metric object is added to the collection at this point
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            ref readonly var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            Assert.Equal(10, metricPointForFirstExport.GetSumLong());

            // Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            metric = exportedItems[0]; // Second Metric object is added to the collection at this point
            metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            var metricPointForSecondExport = metricPointsEnumerator.Current;
            Assert.Equal(25, metricPointForSecondExport.GetSumLong());

            // MetricPoint.LongValue for the first exporter metric should still be 10
            Assert.Equal(10, metricPointForFirstExport.GetSumLong());
        }

        [Fact]
        public void Investigate_2361()
        {
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2361

            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            int i = 0;
            var counterLong = meter.CreateObservableCounter(
                "observable-counter",
                () => ++i * 10);

            meterProvider.ForceFlush();
            Assert.Equal(1, i); // verify that callback is invoked when calling Flush
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong());

            meterProvider.ForceFlush();
            Assert.Equal(2, i); // verify that callback is invoked when calling Flush
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(20, metricPoint2.GetSumLong());

            // Retest 1st item, this is expected to be unchanged.
            Assert.Equal(10, metricPoint1.GetSumLong());
        }

        private static MetricPoint GetSingleMetricPoint(Metric metric)
        {
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            ref readonly var metricPoint = ref metricPointsEnumerator.Current;
            Assert.False(metricPointsEnumerator.MoveNext());
            return metricPoint;
        }
    }
}
