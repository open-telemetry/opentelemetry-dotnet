// <copyright file="MetricsTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricsTest
    {
        [Fact]
        public void LongCounterSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;

            var testCounter = meter.CreateInt64Counter("testCounter");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var labels3 = new List<KeyValuePair<string, string>>();
            labels3.Add(new KeyValuePair<string, string>("dim1", "value3"));

            var context = default(SpanContext);
            testCounter.Add(context, 100, meter.GetLabelSet(labels1));
            testCounter.Add(context, 10, meter.GetLabelSet(labels1));

            var boundCounterLabel2 = testCounter.Bind(labels2);
            boundCounterLabel2.Add(context, 200);

            testCounter.Add(context, 200, meter.GetLabelSet(labels3));
            testCounter.Add(context, 10, meter.GetLabelSet(labels3));

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];

            Assert.Equal("testCounter", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 3 time series, as 3 unique label sets.
            Assert.Equal(3, metric.Data.Count);
            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricLong = metricSeries as Int64SumData;
            Assert.Equal(110, metricLong.Sum);

            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricLong = metricSeries as Int64SumData;
            Assert.Equal(200, metricLong.Sum);

            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value3"));
            metricLong = metricSeries as Int64SumData;
            Assert.Equal(210, metricLong.Sum);
        }

        [Fact]
        public void DoubleCounterSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;

            var testCounter = meter.CreateDoubleCounter("testCounter");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var labels3 = new List<KeyValuePair<string, string>>();
            labels3.Add(new KeyValuePair<string, string>("dim1", "value3"));

            var context = default(SpanContext);
            testCounter.Add(context, 100.2, meter.GetLabelSet(labels1));
            testCounter.Add(context, 10.2, meter.GetLabelSet(labels1));

            var boundCounterLabel2 = testCounter.Bind(labels2);
            boundCounterLabel2.Add(context, 200.2);

            testCounter.Add(context, 200.2, meter.GetLabelSet(labels3));
            testCounter.Add(context, 10.2, meter.GetLabelSet(labels3));

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];

            Assert.Equal("testCounter", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 3 time series, as 3 unique label sets.
            Assert.Equal(3, metric.Data.Count);
            var expectedSum = 100.2 + 10.2;
            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricDouble = metricSeries as DoubleSumData;
            Assert.Equal(expectedSum, metricDouble.Sum);

            expectedSum = 200.2;
            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricDouble = metricSeries as DoubleSumData;
            Assert.Equal(expectedSum, metricDouble.Sum);

            expectedSum = 200.2 + 10.2;
            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value3"));
            metricDouble = metricSeries as DoubleSumData;
            Assert.Equal(expectedSum, metricDouble.Sum);
        }

        [Fact]
        public void LongMeasureSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testMeasure = meter.CreateInt64Measure("testMeasure");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var context = default(SpanContext);
            testMeasure.Record(context, 100, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 10, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 1, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 200, meter.GetLabelSet(labels2));
            testMeasure.Record(context, 20, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];
            Assert.Equal("testMeasure", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 2 time series, as 2 unique label sets.
            Assert.Equal(2, metric.Data.Count);

            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricSummary = metricSeries as Int64SummaryData;
            Assert.Equal(111, metricSummary.Sum);
            Assert.Equal(3, metricSummary.Count);
            Assert.Equal(1, metricSummary.Min);
            Assert.Equal(100, metricSummary.Max);

            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricSummary = metricSeries as Int64SummaryData;
            Assert.Equal(220, metricSummary.Sum);
            Assert.Equal(2, metricSummary.Count);
            Assert.Equal(20, metricSummary.Min);
            Assert.Equal(200, metricSummary.Max);
        }

        [Fact]
        public void DoubleMeasureSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testMeasure = meter.CreateDoubleMeasure("testMeasure");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var context = default(SpanContext);
            testMeasure.Record(context, 100.2, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 10.2, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 1.2, meter.GetLabelSet(labels1));
            testMeasure.Record(context, 200.2, meter.GetLabelSet(labels2));
            testMeasure.Record(context, 20.2, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];
            Assert.Equal("testMeasure", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 2 time series, as 2 unique label sets.
            Assert.Equal(2, metric.Data.Count);

            var expectedSum = 100.2 + 10.2 + 1.2;
            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricSummary = metricSeries as DoubleSummaryData;
            Assert.Equal(expectedSum, metricSummary.Sum);
            Assert.Equal(3, metricSummary.Count);
            Assert.Equal(1.2, metricSummary.Min);
            Assert.Equal(100.2, metricSummary.Max);

            expectedSum = 200.2 + 20.2;
            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricSummary = metricSeries as DoubleSummaryData;
            Assert.Equal(expectedSum, metricSummary.Sum);
            Assert.Equal(2, metricSummary.Count);
            Assert.Equal(20.2, metricSummary.Min);
            Assert.Equal(200.2, metricSummary.Max);
        }

        [Fact]
        public void LongObserverSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testObserver = meter.CreateInt64Observer("testObserver", this.TestCallbackLong);

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];
            Assert.Equal("testObserver", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 2 time series, as 2 unique label sets.
            Assert.Equal(2, metric.Data.Count);

            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricLong = metricSeries as Int64SumData;
            Assert.Equal(30, metricLong.Sum);

            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricLong = metricSeries as Int64SumData;
            Assert.Equal(300, metricLong.Sum);
        }

        [Fact]
        public void DoubleObserverSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testObserver = meter.CreateDoubleObserver("testObserver", this.TestCallbackDouble);

            meter.Collect();

            Assert.Single(testProcessor.Metrics);
            var metric = testProcessor.Metrics[0];
            Assert.Equal("testObserver", metric.MetricName);
            Assert.Equal("library1", metric.MetricNamespace);

            // 2 time series, as 2 unique label sets.
            Assert.Equal(2, metric.Data.Count);

            var metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value1"));
            var metricLong = metricSeries as DoubleSumData;
            Assert.Equal(30.5, metricLong.Sum);

            metricSeries = metric.Data.Single(data => data.Labels.Any(l => l.Key == "dim1" && l.Value == "value2"));
            metricLong = metricSeries as DoubleSumData;
            Assert.Equal(300.5, metricLong.Sum);
        }

        [Fact]
        public void ExportNonEmptyLongMeasure()
        {
            var testProcessor = new TestMetricProcessor();
            var testExporter = new TestMetricExporter(null);
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .SetExporter(testExporter)
                .SetPushInterval(TimeSpan.FromMilliseconds(100))
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testMeasure = meter.CreateInt64Measure("testMeasure");
            var context = default(SpanContext);

            // These 2 measures should be summed into 1 record.

            testMeasure.Record(context, 100, LabelSet.BlankLabelSet);
            testMeasure.Record(context, 200, LabelSet.BlankLabelSet);

            // Let collect/export run multiple time.  Including when no measures are recorded.

            System.Threading.Thread.Sleep(1000);

            int recordCount = testExporter.Metrics.Count;
            int dataCount = testExporter.Metrics.Select(k => k.Data.Count).Sum();
            long measureCount = testExporter.Metrics.Select(k => k.Data.Select(j => ((Int64SummaryData)j).Count).Sum()).Sum();

            Assert.True(recordCount >= 1);
            Assert.Equal(1, dataCount);
            Assert.Equal(2, measureCount);
        }

        [Fact]
        public void ExportNonEmptyDoubleMeasure()
        {
            var testProcessor = new TestMetricProcessor();
            var testExporter = new TestMetricExporter(null);
            var meter = Sdk.CreateMeterProviderBuilder()
                .SetProcessor(testProcessor)
                .SetExporter(testExporter)
                .SetPushInterval(TimeSpan.FromMilliseconds(100))
                .Build()
                .GetMeter("library1") as MeterSdk;
            var testMeasure = meter.CreateDoubleMeasure("testMeasure");
            var context = default(SpanContext);

            // These 2 measures should be summed into 1 record.

            testMeasure.Record(context, 100.1, LabelSet.BlankLabelSet);
            testMeasure.Record(context, 200.2, LabelSet.BlankLabelSet);

            // Let collect/export run multiple time.  Including when no measures are recorded.

            System.Threading.Thread.Sleep(1000);

            int recordCount = testExporter.Metrics.Count;
            int dataCount = testExporter.Metrics.Select(k => k.Data.Count).Sum();
            long measureCount = testExporter.Metrics.Select(k => k.Data.Select(j => ((DoubleSummaryData)j).Count).Sum()).Sum();

            Assert.True(recordCount >= 1);
            Assert.Equal(1, dataCount);
            Assert.Equal(2, measureCount);
        }

        private void TestCallbackLong(Int64ObserverMetric observerMetric)
        {
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            observerMetric.Observe(10, labels1);
            observerMetric.Observe(20, labels1);
            observerMetric.Observe(30, labels1);

            observerMetric.Observe(100, labels2);
            observerMetric.Observe(200, labels2);
            observerMetric.Observe(300, labels2);
        }

        private void TestCallbackDouble(DoubleObserverMetric observerMetric)
        {
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            observerMetric.Observe(10.5, labels1);
            observerMetric.Observe(20.5, labels1);
            observerMetric.Observe(30.5, labels1);

            observerMetric.Observe(100.5, labels2);
            observerMetric.Observe(200.5, labels2);
            observerMetric.Observe(300.5, labels2);
        }
    }
}
