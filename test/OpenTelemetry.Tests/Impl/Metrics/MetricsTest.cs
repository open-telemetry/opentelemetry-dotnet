// <copyright file="MetricsTest.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System.Linq;
using System.Collections.Generic;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Trace;
using Xunit;
using OpenTelemetry.Metrics.Export;
using System.Diagnostics;

namespace OpenTelemetry.Metrics.Test
{
    public class MetricsTest
    {
        [Fact]
        public void CounterSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
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

            Assert.Equal(3, testProcessor.longMetrics.Count);
            Assert.Equal(3, testProcessor.longMetrics.Count(m => m.MetricName == "testCounter"));

            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 110 ));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 200));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 210));            
        }

        [Fact]
        public void MeasureSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
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

            Assert.Equal(2, testProcessor.longMetrics.Count);
            Assert.Equal(2, testProcessor.longMetrics.Count(m => m.MetricName == "testMeasure"));

            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Sum == 111));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Count == 3));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Min == 1));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Max == 100));


            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Sum == 220));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Count == 2));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Min == 20));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SummaryData<long>).Max == 200));
        }

        [Fact]
        public void LongObserverSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testObserver = meter.CreateInt64Observer("testObserver", TestCallbackLong);

            meter.Collect();

            Assert.Equal(2, testProcessor.longMetrics.Count);
            Assert.Equal(2, testProcessor.longMetrics.Count(m => m.MetricName == "testObserver"));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 30));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 300));
        }

        [Fact]
        public void DoubleObserverSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testObserver = meter.CreateDoubleObserver("testObserver", TestCallbackDouble);

            meter.Collect();

            Assert.Equal(2, testProcessor.doubleMetrics.Count);
            Assert.Equal(2, testProcessor.doubleMetrics.Count(m => m.MetricName == "testObserver"));
            Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 30.5));
            Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 300.5));
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
