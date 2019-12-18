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
using System.Collections.Generic;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Metrics.Test
{
    public class MetricsTest
    {
        [Fact(Skip = "TODO: Make robust tests. These are just basic tests to see metric in action.")]
        public void CounterSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateInt64Counter("testCounter");
            
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            testCounter.Add(SpanContext.BlankLocal, 100, meter.GetLabelSet(labels1));
            testCounter.Add(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels1));
            testCounter.Add(SpanContext.BlankLocal, 200, meter.GetLabelSet(labels2));
            testCounter.Add(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.counters.Count);
            Assert.Equal("testCounter", testProcessor.counters[1].Item1);
            Assert.Equal("testCounter", testProcessor.counters[0].Item1);

            Assert.Equal(meter.GetLabelSet(labels1), testProcessor.counters[1].Item2);
            Assert.Equal(meter.GetLabelSet(labels2), testProcessor.counters[0].Item2);

            Assert.Equal(110, testProcessor.counters[1].Item3);
            Assert.Equal(210, testProcessor.counters[0].Item3);
        }

        [Fact(Skip = "TODO: Make robust tests. These are just basic tests to see metric in action.")]
        public void GaugeSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testGauge = meter.CreateInt64Gauge("testGauge");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            testGauge.Set(SpanContext.BlankLocal, 100, meter.GetLabelSet(labels1));
            testGauge.Set(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels1));
            testGauge.Set(SpanContext.BlankLocal, 200, meter.GetLabelSet(labels2));
            testGauge.Set(SpanContext.BlankLocal, 20, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.gauges.Count);
            Assert.Equal("testGauge", testProcessor.gauges[1].Item1);
            Assert.Equal("testGauge", testProcessor.gauges[0].Item1);

            Assert.Equal(meter.GetLabelSet(labels1), testProcessor.gauges[1].Item2);
            Assert.Equal(meter.GetLabelSet(labels2), testProcessor.gauges[0].Item2);

            Assert.Equal(10, testProcessor.gauges[1].Item3.Item1);
            Assert.Equal(20, testProcessor.gauges[0].Item3.Item1);
        }


        [Fact(Skip = "TODO: Make robust tests. These are just basic tests to see metric in action.")]
        public void MeasureSendsAggregateToRegisteredProcessor()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testMeasure = meter.CreateInt64Measure("testMeasure");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            testMeasure.Record(SpanContext.BlankLocal, 100, meter.GetLabelSet(labels1));
            testMeasure.Record(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels1));
            testMeasure.Record(SpanContext.BlankLocal, 200, meter.GetLabelSet(labels2));
            testMeasure.Record(SpanContext.BlankLocal, 20, meter.GetLabelSet(labels2));

            meter.Collect();

            Assert.Equal(2, testProcessor.measures.Count);
            Assert.Equal("testMeasure", testProcessor.measures[1].Item1);
            Assert.Equal("testMeasure", testProcessor.measures[0].Item1);

            Assert.Equal(meter.GetLabelSet(labels1), testProcessor.measures[1].Item2);
            Assert.Equal(meter.GetLabelSet(labels2), testProcessor.measures[0].Item2);

            Assert.Contains(100, testProcessor.measures[1].Item3);
            Assert.Contains(10, testProcessor.measures[1].Item3);


            Assert.Contains(200, testProcessor.measures[0].Item3);
            Assert.Contains(20, testProcessor.measures[0].Item3);
        }
    }
}
