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
        public void Verify_MetricPoint_UsingDeltaAggregation()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.Temporality = AggregationTemporality.Delta;
                })
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems);
            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong());

            // Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems);
            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(25, metricPoint2.GetSumLong());

            // Retest 1st item, this is expected to be unchanged.
            Assert.Equal(10, metricPoint1.GetSumLong());
        }

        [Fact]
        public void Verify_Metric_UsingCounter()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            counter.Add(10);

            meterProvider.ForceFlush();
            Assert.Single(exportedItems);
            var metric1 = exportedItems[0];

            counter.Add(5);
            meterProvider.ForceFlush();
            Assert.Single(exportedItems);
            var metric2 = exportedItems[0];

            // Note that although flush has been called twice
            // the same metric has been exported.
            // This is by design, because the MetricsApi reuses metrics.
            Assert.Same(metric1, metric2);
        }

        [Fact]
        public void Verify_MetricPoint_UsingCounter()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            counter.Add(10);

            meterProvider.ForceFlush();
            Assert.Single(exportedItems);

            counter.Add(5);

            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong()); // Note that the second counter.Add doesn't affect the sum until calling Flush.

            meterProvider.ForceFlush();
            Assert.Single(exportedItems);

            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(15, metricPoint2.GetSumLong()); // The second MetricPoint will have the updated value.

            // Retest 1st item, this is expected to be unchanged.
            Assert.Equal(10, metricPoint1.GetSumLong());
        }

        [Fact]
        public void Verify_MetricPoint_UsingObservableCounter()
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
            Assert.Single(exportedItems);
            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong());

            meterProvider.ForceFlush();
            Assert.Equal(2, i); // verify that callback is invoked when calling Flush
            Assert.Single(exportedItems);
            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(20, metricPoint2.GetSumLong());

            // Retest 1st item, this is expected to be unchanged.
            Assert.Equal(10, metricPoint1.GetSumLong());
        }

        [Fact]
        public void Verify_MetricPoint_UsingHistograms()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var histogram = meter.CreateHistogram<int>("histogram");

            for (int i = 0; i < 5; i++)
            {
                histogram.Record(i);
            }

            meterProvider.ForceFlush();

            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(5, metricPoint1.GetHistogramCount());
            Assert.Equal(10, metricPoint1.GetHistogramSum());

            for (int i = 0; i < 5; i++)
            {
                histogram.Record(i);
            }

            meterProvider.ForceFlush();

            // Note that although metricPoint1 represents the first flush,
            // this struct holds a reference to HistogramBuckets and this value has updated.
            // This is by design.
            Assert.Equal(5, metricPoint1.GetHistogramCount());
            Assert.Equal(20, metricPoint1.GetHistogramSum());
        }

        private static MetricPoint GetSingleMetricPoint(Metric metric)
        {
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // Only one MetricPoint is emitted for the Metric
            var metricPoint = metricPointsEnumerator.Current;
            Assert.False(metricPointsEnumerator.MoveNext());
            return metricPoint;
        }
    }
}
