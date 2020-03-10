// <copyright file="MinMaxSumCountAggregatorTest.cs" company="OpenTelemetry Authors">
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
    public class MinMaxSumCountAggregatorTest
    {
        private class UpdateThreadArguments<T> where T: struct
        {
            public ManualResetEvent mreToBlockUpdateThread;
            public ManualResetEvent mreToEnsureAllThreadsStart;
            public int threadsStartedCount;
            public MeasureMinMaxSumCountAggregator<T> minMaxSumCountAggregator;
        }

        [Fact]
        public void MeasureAggregatorSupportsLong()
        {
            MeasureMinMaxSumCountAggregator<long> aggregator = new MeasureMinMaxSumCountAggregator<long>();            
        }

        [Fact]
        public void MeasureAggregatorSupportsDouble()
        {
            MeasureMinMaxSumCountAggregator<double> aggregator = new MeasureMinMaxSumCountAggregator<double>();
        }

        [Fact]
        public void MeasureAggregatorConstructorThrowsForUnSupportedTypeInt()
        {
            Assert.Throws<Exception>(() => new MeasureMinMaxSumCountAggregator<int>());            
        }

        [Fact]
        public void MeasureAggregatorConstructorThrowsForUnSupportedTypeByte()
        {
            Assert.Throws<Exception>(() => new MeasureMinMaxSumCountAggregator<byte>());
        }

        [Fact]
        public void MeasureAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesLong()
        {
            // create an aggregator
            MeasureMinMaxSumCountAggregator<long> aggregator = new MeasureMinMaxSumCountAggregator<long>();
            var summary = aggregator.ToMetricData() as SummaryData<long>;

            // we start with 0.
            Assert.Equal(0, summary.Sum);
            Assert.Equal(0, summary.Count);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments<long>();
            argToThread.minMaxSumCountAggregator = aggregator;
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
            summary = aggregator.ToMetricData() as SummaryData<long>;

            // 1000000 times (10+50+100) by each thread. times 10 as there are 10 threads
            Assert.Equal(1600000000, summary.Sum);

            // 1000000 times 3 by each thread, times 10 as there are 10 threads.
            Assert.Equal(30000000, summary.Count);

            // Min and Max are 10 and 100
            Assert.Equal(10, summary.Min);
            Assert.Equal(100, summary.Max);
        }

        [Fact]
        public void MeasureAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdatesDouble()
        {
            // create an aggregator
            MeasureMinMaxSumCountAggregator<double> aggregator = new MeasureMinMaxSumCountAggregator<double>();
            var summary = aggregator.ToMetricData() as SummaryData<double>;

            // we start with 0.
            Assert.Equal(0, summary.Sum);
            Assert.Equal(0, summary.Count);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments<double>();
            argToThread.minMaxSumCountAggregator = aggregator;
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
            summary = aggregator.ToMetricData() as SummaryData<double>;

            // 1000000 times (10+50+100) by each thread. times 10 as there are 10 threads
            Assert.Equal(1600000000, summary.Sum);

            // 1000000 times 3 by each thread, times 10 as there are 10 threads.
            Assert.Equal(30000000, summary.Count);

            // Min and Max are 10 and 100
            Assert.Equal(10, summary.Min);
            Assert.Equal(100, summary.Max);
        }


        private static void LongMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<long>;
            var mre = arguments.mreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.mreToEnsureAllThreadsStart;
            var agg = arguments.minMaxSumCountAggregator;

            if (Interlocked.Increment(ref arguments.threadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10);
                agg.Update(50);
                agg.Update(100);
            }
        }

        private static void DoubleMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<double>;
            var mre = arguments.mreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.mreToEnsureAllThreadsStart;
            var agg = arguments.minMaxSumCountAggregator;

            if (Interlocked.Increment(ref arguments.threadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10.0);
                agg.Update(50.0);
                agg.Update(100.0);
            }
        }
    }
}
