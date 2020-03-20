// <copyright file="CounterCleanUpTests.cs" company="OpenTelemetry Authors">
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
    public class CounterCleanUpTests
    {
        [Fact]
        public void CounterBoundInstrumentsStatusUpdatedCorrectlySingleThread()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateInt64Counter("testCounter") as CounterMetricSdk<long>;

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            var ls1 = meter.GetLabelSet(labels1);

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));
            var ls2 = meter.GetLabelSet(labels2);

            var labels3 = new List<KeyValuePair<string, string>>();
            labels3.Add(new KeyValuePair<string, string>("dim1", "value3"));
            var ls3 = meter.GetLabelSet(labels3);

            var context = default(SpanContext);

            // We have ls1, ls2, ls3
            // ls1 and ls3 are not bound so they should removed when no usage for a Collect cycle.
            // ls2 is bound by user.
            testCounter.Add(context, 100, ls1);
            testCounter.Add(context, 10, ls1);
            // initial status for temp bound instruments are UpdatePending.            
            Assert.Equal(RecordStatus.UpdatePending, testCounter.GetAllBoundInstruments()[ls1].Status);

            var boundCounterLabel2 = testCounter.Bind(ls2);
            boundCounterLabel2.Add(context, 200);
            // initial status for user bound instruments are Bound.
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);

            testCounter.Add(context, 200, ls3);
            testCounter.Add(context, 10, ls3);
            // initial status for temp bound instruments are UpdatePending.            
            Assert.Equal(RecordStatus.UpdatePending, testCounter.GetAllBoundInstruments()[ls3].Status);

            // This collect should mark ls1, ls3 as CandidateForRemoval, leave ls2 untouched.
            meter.Collect();

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls3].Status);
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);

            // Use ls1 again, so that it'll be promoted to UpdatePending
            testCounter.Add(context, 100, ls1);

            // This collect should mark ls1 as CandidateForRemoval, leave ls2 untouched.
            // And physically remove ls3, as it was not used since last Collect
            meter.Collect();

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);            
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);
            Assert.False(testCounter.GetAllBoundInstruments().ContainsKey(ls3));
        }        
    }
}
