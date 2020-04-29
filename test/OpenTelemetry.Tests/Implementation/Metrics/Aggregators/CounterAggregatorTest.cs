// <copyright file="CounterAggregatorTest.cs" company="OpenTelemetry Authors">
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
using System;
using System.Threading;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace OpenTelemetry.Metrics.Test
{
    public class CounterAggregatorTest
    {
        private class UpdateThreadArguments<T> where T : struct
        {
            public ManualResetEvent mreToBlockUpdateThread;
            public ManualResetEvent mreToEnsureAllThreadsStart;
            public int threadsStartedCount;
            public Aggregator<T> counterSumAggregator;
        }

        [Fact]
        public void CounterAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesLong()
        {
            // create an aggregator
            var aggregator = new Int64CounterSumAggregator();
            var sum = aggregator.ToMetricData() as Int64SumData;

            // we start with 0.
            Assert.Equal(0, sum.Sum);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread =
                new UpdateThreadArguments<long>
                {
                    counterSumAggregator = aggregator,
                    threadsStartedCount = 0,
                    mreToBlockUpdateThread = mre,
                    mreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart,
                };

            Thread[] t = new Thread[10];
            for (int i = 0; i < 10; i++)
            {
                t[i] = new Thread(LongMetricUpdateThread);
                t[i].Start(argToThread);
            }

            // Block until all 10 threads started.
            mreToEnsureAllThreadsStart.WaitOne();

            // kick-off all the threads.
            mre.Set();

            for (int i = 0; i < 10; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            // check point.
            aggregator.Checkpoint();
            sum = aggregator.ToMetricData() as Int64SumData;

            // 1000000 times 10 by each thread. times 10 as there are 10 threads
            Assert.Equal(100000000, sum.Sum);
        }

        [Fact]
        public void CounterAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesDouble()
        {
            // create an aggregator
            var aggregator = new DoubleCounterSumAggregator();
            var sum = aggregator.ToMetricData() as DoubleSumData;

            // we start with 0.0
            Assert.Equal(0.0, sum.Sum);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread =
                new UpdateThreadArguments<double>
                {
                    counterSumAggregator = aggregator,
                    threadsStartedCount = 0,
                    mreToBlockUpdateThread = mre,
                    mreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart,
                };

            Thread[] t = new Thread[10];
            for (int i = 0; i < 10; i++)
            {
                t[i] = new Thread(DoubleMetricUpdateThread);
                t[i].Start(argToThread);
            }

            // Block until all 10 threads started.
            mreToEnsureAllThreadsStart.WaitOne();

            // kick-off all the threads.
            mre.Set();

            for (int i = 0; i < 10; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            // check point.
            aggregator.Checkpoint();
            sum = aggregator.ToMetricData() as DoubleSumData;

            // 1000000 times 10.5 by each thread. times 10 as there are 10 threads
            Assert.Equal(105000000, sum.Sum);
        }


        private static void LongMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<long>;
            var mre = arguments.mreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.mreToEnsureAllThreadsStart;
            var agg = arguments.counterSumAggregator;

            if (Interlocked.Increment(ref arguments.threadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10);
            }
        }

        private static void DoubleMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<double>;
            var mre = arguments.mreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.mreToEnsureAllThreadsStart;
            var agg = arguments.counterSumAggregator;

            if (Interlocked.Increment(ref arguments.threadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10.5);
            }
        }
    }
}
