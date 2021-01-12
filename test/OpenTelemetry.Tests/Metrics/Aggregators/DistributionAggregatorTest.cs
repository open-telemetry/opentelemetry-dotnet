// <copyright file="DistributionAggregatorTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class DistributionAggregatorTest
    {
        [Fact]
        public void Int64MeasureDistributionAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdate()
        {
            // create an aggregator
            var aggregator = new Int64MeasureDistributionAggregator(new Int64ExplicitDistributionOptions
            {
                Bounds = new long[] { 0, 1 }
            });
            var distributionData = aggregator.ToMetricData() as Int64DistributionData;

            // we start with 0.
            Assert.Equal(0, distributionData.Count);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread =
                new UpdateThreadArguments<long>
                {
                    DistributionAggregator = aggregator,
                    ThreadsStartedCount = 0,
                    MreToBlockUpdateThread = mre,
                    MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart,
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
            distributionData = aggregator.ToMetricData() as Int64DistributionData;

            Assert.Equal(10000000, distributionData.Count);
            Assert.Equal(-1, distributionData.Min);
            Assert.Equal(98, distributionData.Max);
        }

        [Fact]
        public void DoubleMeasureDistributionAggregatorAggregatesCorrectlyWhenMultipleThreadsUpdate()
        {
            // create an aggregator
            var aggregator = new DoubleMeasureDistributionAggregator(new DoubleExplicitDistributionOptions
            {
                Bounds = new double[] { 0, 1 },
            });
            var distributionData = aggregator.ToMetricData() as DoubleDistributionData;

            // we start with 0.
            Assert.Equal(0, distributionData.Count);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread =
                new UpdateThreadArguments<double>
                {
                    DistributionAggregator = aggregator,
                    ThreadsStartedCount = 0,
                    MreToBlockUpdateThread = mre,
                    MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart,
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
            distributionData = aggregator.ToMetricData() as DoubleDistributionData;

            Assert.Equal(10000000, distributionData.Count);
            Assert.Equal(-1, distributionData.Min);
            Assert.Equal(98, distributionData.Max);
        }

        private static void LongMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<long>;
            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var agg = arguments.DistributionAggregator;

            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update((i % 100) - 1);
            }
        }

        private static void DoubleMetricUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments<double>;
            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var agg = arguments.DistributionAggregator;

            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update((i % 100) - 1);
            }
        }

        private class UpdateThreadArguments<T>
            where T : struct
        {
            public ManualResetEvent MreToBlockUpdateThread;
            public ManualResetEvent MreToEnsureAllThreadsStart;
            public int ThreadsStartedCount;
            public Aggregator<T> DistributionAggregator;
        }
    }
}
