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

            var context = default(SpanContext);
            testCounter.Add(context, 100, meter.GetLabelSet(labels1));
            testCounter.Add(context, 10, meter.GetLabelSet(labels1));
            testCounter.Add(context, 200, meter.GetLabelSet(labels2));
            testCounter.Add(context, 10, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.longMetrics.Count);
            Assert.Equal(2, testProcessor.longMetrics.Count(m => m.MetricName == "testCounter"));

            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 110 ));
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
        public void ObserverSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testObserver = meter.CreateInt64Observer("testObserver");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var context = default(SpanContext);
            testObserver.Observe(context, 100, meter.GetLabelSet(labels1));
            testObserver.Observe(context, 10, meter.GetLabelSet(labels1));
            testObserver.Observe(context, 200, meter.GetLabelSet(labels2));
            testObserver.Observe(context, 20, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.longMetrics.Count);
            Assert.Equal(2, testProcessor.longMetrics.Count(m => m.MetricName == "testObserver"));

            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 10));
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 20));
        }
    }
}
