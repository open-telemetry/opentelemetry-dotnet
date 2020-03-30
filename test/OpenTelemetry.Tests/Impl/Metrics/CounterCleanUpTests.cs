﻿// <copyright file="CounterCleanUpTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System;

namespace OpenTelemetry.Metrics.Test
{
    public class CounterCleanUpTests
    {
        [Fact]
        public void LongCounterBoundInstrumentsStatusUpdatedCorrectlySingleThread()
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
            // initial/forever status for user bound instruments are Bound.
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

        [Fact]
        public void DoubleCounterBoundInstrumentsStatusUpdatedCorrectlySingleThread()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateDoubleCounter("testCounter") as CounterMetricSdk<double>;

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
            testCounter.Add(context, 100.0, ls1);
            testCounter.Add(context, 10.0, ls1);
            // initial status for temp bound instruments are UpdatePending.            
            Assert.Equal(RecordStatus.UpdatePending, testCounter.GetAllBoundInstruments()[ls1].Status);

            var boundCounterLabel2 = testCounter.Bind(ls2);
            boundCounterLabel2.Add(context, 200.0);
            // initial/forever status for user bound instruments are Bound.
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);

            testCounter.Add(context, 200.0, ls3);
            testCounter.Add(context, 10.0, ls3);
            // initial status for temp bound instruments are UpdatePending.            
            Assert.Equal(RecordStatus.UpdatePending, testCounter.GetAllBoundInstruments()[ls3].Status);

            // This collect should mark ls1, ls3 as CandidateForRemoval, leave ls2 untouched.
            meter.Collect();

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls3].Status);
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);

            // Use ls1 again, so that it'll be promoted to UpdatePending
            testCounter.Add(context, 100.0, ls1);

            // This collect should mark ls1 as CandidateForRemoval, leave ls2 untouched.
            // And physically remove ls3, as it was not used since last Collect
            meter.Collect();

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);
            Assert.Equal(RecordStatus.Bound, testCounter.GetAllBoundInstruments()[ls2].Status);
            Assert.False(testCounter.GetAllBoundInstruments().ContainsKey(ls3));
        }

        [Fact]
        public void LongCounterBoundInstrumentsStatusUpdatedCorrectlyMultiThread()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateInt64Counter("testCounter") as CounterMetricSdk<long>;

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            var ls1 = meter.GetLabelSet(labels1);

            var context = default(SpanContext);
            
            testCounter.Add(context, 100, ls1);
            testCounter.Add(context, 10, ls1);
            
            // This collect should mark ls1 CandidateForRemoval
            meter.Collect();
            Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 110));

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);

            // Call Collect() and use LS1 parallelly.
            var mre = new ManualResetEvent(false);
            var argsForMeterCollect = new ArgsToThread();
            argsForMeterCollect.mreToBlockStartOfThread = mre;
            argsForMeterCollect.callback = () => meter.Collect();

            var argsForCounterAdd = new ArgsToThread();
            argsForCounterAdd.mreToBlockStartOfThread = mre;
            argsForCounterAdd.callback = () => testCounter.Add(context, 100, ls1);

            var collectThread = new Thread(ThreadMethod);
            var updateThread = new Thread(ThreadMethod);
            collectThread.Start(argsForMeterCollect);
            updateThread.Start(argsForCounterAdd);

            // Attempt to start both threads.
            // TODO:
            // Instead of this, evaluate if a different testing approach is needed.
            // One or more thread doing Updates in parallel.
            // One thread doing occasional Collect.
            // At the end, validate that no metric update is lost.
            mre.Set();

            collectThread.Join();
            updateThread.Join();

            // Validate            
            if(testCounter.GetAllBoundInstruments()[ls1].Status == RecordStatus.CandidateForRemoval)
            {
                // If record is marked candidate for removal, then it must be already exported to processor.
                Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 100));
            }
            else
            {
                // Record not yet marked for removal. So exported record has 0 sum.
                Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 0));

                // The following collect should collect the metric update.
                meter.Collect();
                Assert.Single(testProcessor.longMetrics.Where(m => (m.Data as SumData<long>).Sum == 100));
                Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);
            }
        }

        [Fact]
        public void DoubleCounterBoundInstrumentsStatusUpdatedCorrectlyMultiThread()
        {
            var testProcessor = new TestMetricProcessor();
            var meter = MeterFactory.Create(testProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateDoubleCounter("testCounter") as CounterMetricSdk<double>;

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            var ls1 = meter.GetLabelSet(labels1);

            var context = default(SpanContext);

            testCounter.Add(context, 100.0, ls1);
            testCounter.Add(context, 10.0, ls1);

            // This collect should mark ls1 CandidateForRemoval
            meter.Collect();
            Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 110.0));

            // Validate collect() has marked records correctly.
            Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);

            // Call Collect() and use LS1 parallelly.
            var mre = new ManualResetEvent(false);
            var argsForMeterCollect = new ArgsToThread();
            argsForMeterCollect.mreToBlockStartOfThread = mre;
            argsForMeterCollect.callback = () => meter.Collect();

            var argsForCounterAdd = new ArgsToThread();
            argsForCounterAdd.mreToBlockStartOfThread = mre;
            argsForCounterAdd.callback = () => testCounter.Add(context, 100.0, ls1);

            var collectThread = new Thread(ThreadMethod);
            var updateThread = new Thread(ThreadMethod);
            collectThread.Start(argsForMeterCollect);
            updateThread.Start(argsForCounterAdd);

            // Attempt to start both threads.
            // TODO:
            // Instead of this, evaluate if a different testing approach is needed.
            // One or more thread doing Updates in parallel.
            // One thread doing occasional Collect.
            // At the end, validate that no metric update is lost.
            mre.Set();

            collectThread.Join();
            updateThread.Join();

            // Validate            
            if (testCounter.GetAllBoundInstruments()[ls1].Status == RecordStatus.CandidateForRemoval)
            {
                // If record is marked candidate for removal, then it must be already exported to processor.
                Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 100.0));
            }
            else
            {
                // Record not yet marked for removal. So exported record has 0 sum.
                Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 0));

                // The following collect should collect the metric update.
                meter.Collect();
                Assert.Single(testProcessor.doubleMetrics.Where(m => (m.Data as SumData<double>).Sum == 100.0));
                Assert.Equal(RecordStatus.CandidateForRemoval, testCounter.GetAllBoundInstruments()[ls1].Status);
            }
        }

        private static void ThreadMethod(object obj)
        {
            var args = obj as ArgsToThread;
            var mre = args.mreToBlockStartOfThread;
            var callBack = args.callback;

            // Wait until signalled to call Collect.
            mre.WaitOne();
            callBack();
        }
    }

    class ArgsToThread
    {
        public ManualResetEvent mreToBlockStartOfThread;
        public Action callback;
    }
}
