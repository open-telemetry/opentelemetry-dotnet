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
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong());

            // Emit 25 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(25, metricPoint2.GetSumLong());

            // MetricPoint.LongValue for the first exporter metric should still be 10
            Assert.Equal(10, metricPoint1.GetSumLong());
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

        [Fact]
        public void InvestigateCounters()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter1 = meter.CreateCounter<long>("counter1");

            counter1.Add(10);
            meterProvider.ForceFlush();
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metricPoint1 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(10, metricPoint1.GetSumLong());

            var counter2 = meter.CreateCounter<long>("counter2");
            counter1.Add(20);
            counter2.Add(35);

            meterProvider.ForceFlush();

            Assert.Equal(2, exportedItems.Count); // verify that List<metrics> contains 2 items

            var metricPoint2 = GetSingleMetricPoint(exportedItems[0]);
            Assert.Equal(30, metricPoint2.GetSumLong());

            var metricPoint3 = GetSingleMetricPoint(exportedItems[1]);
            Assert.Equal(35, metricPoint3.GetSumLong());
        }

        [Fact]
        public void InvestigateCounter_WithSecondFlush()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metric1 = exportedItems[0];

            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metric2 = exportedItems[0];

            Assert.Same(metric1, metric2);

            var metricPoint1 = GetSingleMetricPoint(metric1);
            Assert.Equal(35, metricPoint1.GetSumLong());

            var metricPoint2 = GetSingleMetricPoint(metric2);
            Assert.Equal(35, metricPoint2.GetSumLong());
        }

        [Fact]
        public void InvestigateCounter_WithoutSecondFlush()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();
            Assert.Single(exportedItems); // verify that List<metrics> contains 1 item
            var metric1 = exportedItems[0];

            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            var metricPoint1 = GetSingleMetricPoint(metric1);
            Assert.Equal(10, metricPoint1.GetSumLong()); // Note that the second counter.Add doesn't affect the sum yet.
        }

        [Fact]
        public void InvestigateEnumerator_UsingCounter()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            // Emit 10 for the MetricPoint with a single key-vaue pair: ("tag1", "value1")
            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));

            meterProvider.ForceFlush();

            var metricPointsAccessor = exportedItems[0].GetMetricPoints();

            counter.Add(25, new KeyValuePair<string, object>("tag1", "value1"));

            var metricPointsEnumerator1 = metricPointsAccessor.GetEnumerator();
            metricPointsEnumerator1.MoveNext();
            var metricPoint1 = metricPointsEnumerator1.Current;
            Assert.Equal(10, metricPoint1.GetSumLong());

            meterProvider.ForceFlush();

            var metricPointsEnumerator2 = metricPointsAccessor.GetEnumerator();
            metricPointsEnumerator2.MoveNext();
            var metricPoint2 = metricPointsEnumerator2.Current;
            Assert.Equal(35, metricPoint2.GetSumLong());

            var metricPoint1again = metricPointsEnumerator1.Current;
            Assert.Equal(35, metricPoint1again.GetSumLong());
        }

        [Fact]
        public void InvestigateEnumerator_UsingObservable()
        {
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

            var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();

            meterProvider.ForceFlush();

            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;
            Assert.Equal(20, metricPoint.GetSumLong());
        }

        [Fact]
        public void TestHistograms()
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
            Assert.Equal(5, metricPoint1.GetHistogramCount());
            Assert.Equal(20, metricPoint1.GetHistogramSum());
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
