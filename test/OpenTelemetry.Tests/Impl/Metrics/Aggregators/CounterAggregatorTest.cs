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
using Xunit;

namespace OpenTelemetry.Metrics.Test
{
    public class CounterAggregatorTest
    {
        private class UpdateThreadArguments<T> where T: struct
        {
            public ManualResetEvent mreToBlockUpdateThread;
            public ManualResetEvent mreToEnsureAllThreadsStart;
            public int threadsStartedCount;
            public CounterSumAggregator<T> counterSumAggregator;
        }

        [Fact]
        public void CounterAggregatorSupportsLong()
        {            
            CounterSumAggregator<long> aggregator = new CounterSumAggregator<long>();            
        }

        [Fact]
        public void CounterAggregatorSupportsDouble()
        {
            CounterSumAggregator<double> aggregator = new CounterSumAggregator<double>();
        }

        [Fact]
        public void CounterAggregatorConstructorThrowsForUnSupportedTypeInt()
        {
            Assert.Throws<Exception>(() => new CounterSumAggregator<int>());            
        }

        [Fact]
        public void CounterAggregatorConstructorThrowsForUnSupportedTypeByte()
        {
            Assert.Throws<Exception>(() => new CounterSumAggregator<byte>());
        }

        [Fact]
        public void CounterAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesLong()
        {
            // create an aggregator
            CounterSumAggregator<long> aggregator = new CounterSumAggregator<long>();
            var sum = aggregator.ValueFromLastCheckpoint();

            // we start with 0.
            Assert.Equal(0, sum);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments<long>();
            argToThread.counterSumAggregator = aggregator;
            argToThread.threadsStartedCount = 0;
            argToThread.mreToBlockUpdateThread = mre;
            argToThread.mreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart;
            
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
            sum = aggregator.ValueFromLastCheckpoint();

            // 1000000 times 10 by each thread. times 10 as there are 10 threads
            Assert.Equal(100000000, sum);
        }

        [Fact]
        public void CounterAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesDouble()
        {
            // create an aggregator
            CounterSumAggregator<double> aggregator = new CounterSumAggregator<double>();
            var sum = aggregator.ValueFromLastCheckpoint();

            // we start with 0.0
            Assert.Equal(0.0, sum);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);            

            var argToThread = new UpdateThreadArguments<double>();
            argToThread.counterSumAggregator = aggregator;
            argToThread.threadsStartedCount = 0;
            argToThread.mreToBlockUpdateThread = mre;
            argToThread.mreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart;

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
            sum = aggregator.ValueFromLastCheckpoint();

            // 1000000 times 10.5 by each thread. times 10 as there are 10 threads
            Assert.Equal(105000000, sum);
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
