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
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class InMemoryExporterTests
    {
        [Fact(Skip = "To be run after https://github.com/open-telemetry/opentelemetry-dotnet/issues/2361 is fixed")]
        public void InMemoryExporterShouldDeepCopyMetricPoints()
        {
            var meter = new Meter("InMemoryExporterTests", "1.0");

            var exportedItems = new List<Metric>();
            using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
            {
                PreferredAggregationTemporality = AggregationTemporality.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("InMemoryExporterTests")
            .AddReader(inMemoryReader)
            .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            // Pull metric data from AggregatorStore
            inMemoryReader.Collect();

            var metric = exportedItems[0]; // Only one Metric object is added to the collection at this point
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            ref var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            Assert.Equal(10, metricPointForFirstExport.LongValue);

            // Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            // Pull metric data from AggregatorStore
            inMemoryReader.Collect();

            metric = exportedItems[1]; // Second Metric object is added to the collection at this point
            metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            var metricPointForSecondExport = metricPointsEnumerator.Current;
            Assert.Equal(25, metricPointForSecondExport.LongValue);

            // MetricPoint.LongValue for the first exporter metric should still be 10
            Assert.Equal(10, metricPointForFirstExport.LongValue);
        }
    }
}
