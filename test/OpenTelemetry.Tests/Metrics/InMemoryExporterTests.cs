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

            // TEST 1: Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
            var metric1 = exportedItems[0]; // Only one Metric object is added to the collection at this point
            Assert.Single(metric1.MetricPoints);
            Assert.Equal(10, metric1.MetricPoints[0].GetSumLong());

            // TEST 2: Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            Assert.Equal(2, exportedItems.Count);
            var metric2 = exportedItems[1]; // Second Metric object is added to the collection at this point
            Assert.Single(metric2.MetricPoints);
            Assert.Equal(25, metric2.MetricPoints[0].GetSumLong());

            // TEST 3: Verify first exported metric is unchanged
            // MetricPoint.LongValue for the first exported metric should still be 10
            Assert.Equal(10, metric1.MetricPoints[0].GetSumLong());
        }
    }
}
