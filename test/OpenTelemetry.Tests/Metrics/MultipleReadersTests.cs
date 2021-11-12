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

using System;
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
            var exporterdMetricItems1 = new List<Metric>();
            using var deltaMetricExporter1 = new InMemoryExporter<Metric>(exporterdMetricItems1);
            using var deltaMetricReader1 = new BaseExportingMetricReader(deltaMetricExporter1)
            {
                PreferredAggregationTemporality = AggregationTemporality.Delta,
            };

            var exporterdMetricItems2 = new List<Metric>();
            using var deltaMetricExporter2 = new InMemoryExporter<Metric>(exporterdMetricItems2);
            using var deltaMetricReader2 = new BaseExportingMetricReader(deltaMetricExporter2)
            {
                PreferredAggregationTemporality = aggregationTemporality,
            };
            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{aggregationTemporality}.{hasViews}");

            var counter = meter.CreateCounter<long>("counter");

            int index = 0;
            var values = new long[] { 100, 200, 300, 400 };
            long GetValue() => values[index++];
            var gauge = meter.CreateObservableGauge("gauge", () => GetValue());

            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(deltaMetricReader1)
                .AddReader(deltaMetricReader2);

            if (hasViews)
            {
                meterProviderBuilder.AddView("counter", "renamedCounter");
            }

            using var meterProvider = meterProviderBuilder.Build();

            counter.Add(10, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Equal(2, exporterdMetricItems1.Count);
            Assert.Equal(2, exporterdMetricItems2.Count);

            // Check value exported for Counter
            this.AssertLongSumValueForMetric(exporterdMetricItems1[0], 10);
            this.AssertLongSumValueForMetric(exporterdMetricItems2[0], 10);

            // Check value exported for Gauge
            this.AssertLongSumValueForMetric(exporterdMetricItems1[1], 100);
            this.AssertLongSumValueForMetric(exporterdMetricItems2[1], 200);

            exporterdMetricItems1.Clear();
            exporterdMetricItems2.Clear();

            counter.Add(15, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Equal(2, exporterdMetricItems1.Count);
            Assert.Equal(2, exporterdMetricItems2.Count);

            // Check value exported for Counter
            this.AssertLongSumValueForMetric(exporterdMetricItems1[0], 15);
            if (aggregationTemporality == AggregationTemporality.Delta)
            {
                this.AssertLongSumValueForMetric(exporterdMetricItems2[0], 15);
            }
            else
            {
                this.AssertLongSumValueForMetric(exporterdMetricItems2[0], 25);
            }

            // Check value exported for Gauge
            this.AssertLongSumValueForMetric(exporterdMetricItems1[1], 300);
            this.AssertLongSumValueForMetric(exporterdMetricItems2[1], 400);
        }

        private void AssertLongSumValueForMetric(Metric metric, long value)
        {
            using var metricPoints = metric.GetMetricPoints();
            var metricPointsEnumerator = metricPoints.GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            ref var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            Assert.Equal(value, metricPointForFirstExport.LongValue);
        }
    }
}
