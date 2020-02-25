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

            Assert.Equal(2, testProcessor.counters.Count);
            Assert.Equal(2, testProcessor.counters.Count(kvp => kvp.Item1 == "testCounter"));

            Assert.Single(testProcessor.counters.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels1))));
            Assert.Single(testProcessor.counters.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels2))));

            Assert.Single(testProcessor.counters.Where(kvp => kvp.Item3 == 110));
            Assert.Single(testProcessor.counters.Where(kvp => kvp.Item3 == 210));
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
            testMeasure.Record(context, 200, meter.GetLabelSet(labels2));
            testMeasure.Record(context, 20, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.measures.Count);
            Assert.Equal(2, testProcessor.measures.Count(kvp => kvp.Item1 == "testMeasure"));

            Assert.Single(testProcessor.measures.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels1))));
            Assert.Single(testProcessor.measures.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels2))));

            Assert.Single(testProcessor.measures.Where(kvp => kvp.Item3.Contains(100) && kvp.Item3.Contains(10)));
            Assert.Single(testProcessor.measures.Where(kvp => kvp.Item3.Contains(200) && kvp.Item3.Contains(20)));
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

            Assert.Equal(2, testProcessor.observations.Count);
            Assert.Equal(2, testProcessor.observations.Count(kvp => kvp.Item1 == "testObserver"));

            Assert.Single(testProcessor.observations.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels1))));
            Assert.Single(testProcessor.observations.Where(kvp => kvp.Item2.Equals(meter.GetLabelSet(labels2))));

            Assert.Single(testProcessor.observations.Where(kvp => kvp.Item3 == 10));
            Assert.Single(testProcessor.observations.Where(kvp => kvp.Item3 == 20));
        }
    }
}
