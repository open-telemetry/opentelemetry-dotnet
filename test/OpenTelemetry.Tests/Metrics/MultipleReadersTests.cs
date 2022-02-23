// <copyright file="MultipleReadersTests.cs" company="OpenTelemetry Authors">
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
    public class MultipleReadersTests
    {
        [Theory]
        [InlineData(AggregationTemporality.Delta, false)]
        [InlineData(AggregationTemporality.Delta, true)]
        [InlineData(AggregationTemporality.Cumulative, false)]
        [InlineData(AggregationTemporality.Cumulative, true)]
        public void SdkSupportsMultipleReaders(AggregationTemporality aggregationTemporality, bool hasViews)
        {
            var exportedItems1 = new List<Metric>();
            var exportedItems2 = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{aggregationTemporality}.{hasViews}");

            var counter = meter.CreateCounter<long>("counter");

            int index = 0;
            var values = new long[] { 100, 200, 300, 400 };
            long GetValue() => values[index++];
            var gauge = meter.CreateObservableGauge("gauge", () => GetValue());

            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems1, metricReaderOptions =>
                {
                    metricReaderOptions.Temporality = AggregationTemporality.Delta;
                })
                .AddInMemoryExporter(exportedItems2, metricReaderOptions =>
                {
                    metricReaderOptions.Temporality = aggregationTemporality;
                });

            if (hasViews)
            {
                meterProviderBuilder.AddView("counter", "renamedCounter");
            }

            using var meterProvider = meterProviderBuilder.Build();

            counter.Add(10, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Equal(2, exportedItems1.Count);
            Assert.Equal(2, exportedItems2.Count);

            // Check value exported for Counter
            this.AssertLongSumValueForMetric(exportedItems1[0], 10);
            this.AssertLongSumValueForMetric(exportedItems2[0], 10);

            // Check value exported for Gauge
            this.AssertLongSumValueForMetric(exportedItems1[1], 100);
            this.AssertLongSumValueForMetric(exportedItems2[1], 200);

            exportedItems1.Clear();
            exportedItems2.Clear();

            counter.Add(15, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Equal(2, exportedItems1.Count);
            Assert.Equal(2, exportedItems2.Count);

            // Check value exported for Counter
            this.AssertLongSumValueForMetric(exportedItems1[0], 15);
            if (aggregationTemporality == AggregationTemporality.Delta)
            {
                this.AssertLongSumValueForMetric(exportedItems2[0], 15);
            }
            else
            {
                this.AssertLongSumValueForMetric(exportedItems2[0], 25);
            }

            // Check value exported for Gauge
            this.AssertLongSumValueForMetric(exportedItems1[1], 300);
            this.AssertLongSumValueForMetric(exportedItems2[1], 400);
        }

        private void AssertLongSumValueForMetric(Metric metric, long value)
        {
            var metricPoints = metric.GetMetricPoints();
            var metricPointsEnumerator = metricPoints.GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            ref readonly var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            if (metric.MetricType.IsSum())
            {
                Assert.Equal(value, metricPointForFirstExport.GetSumLong());
            }
            else
            {
                Assert.Equal(value, metricPointForFirstExport.GetGaugeLastValueLong());
            }
        }
    }
}
