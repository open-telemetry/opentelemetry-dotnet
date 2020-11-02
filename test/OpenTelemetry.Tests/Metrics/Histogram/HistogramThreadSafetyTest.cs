// <copyright file="ExplicitHistogramTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Histogram;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class HistogramThreadSafetyTest
    {
        [Fact]
        public void HistogramAggregatesCorrectlyWhenMultipleThreadsUpdate()
        {
            // create an histogram
            var histogram = new Int64ExplicitHistogram(new long[] { 0, 1 });
            var distributionData = histogram.GetDistributionAndClear();

            // we start with 0.
            Assert.Equal(0, distributionData.Count);

            // setup args to threads.
            var mre = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStart = new ManualResetEvent(false);

            var argToThread =
                new RecordValueArguments
                {
                    Histogram = histogram,
                    ThreadsStartedCount = 0,
                    MreToBlockUpdateThread = mre,
                    MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStart,
                };

            Thread[] t = new Thread[10];
            for (int i = 0; i < 10; i++)
            {
                t[i] = new Thread(RecordValueThread);
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

            distributionData = histogram.GetDistributionAndClear();

            Assert.Equal(10000000, distributionData.Count);
            Assert.Equal(3333340, distributionData.BucketCounts[0]);
            Assert.Equal(3333330, distributionData.BucketCounts[1]);
            Assert.Equal(3333330, distributionData.BucketCounts[2]);
        }

        private static void RecordValueThread(object obj)
        {
            var arguments = obj as RecordValueArguments;
            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var histogram = arguments.Histogram;

            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == 10)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling RecordValue
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                histogram.RecordValue((i % 3) - 1);
            }
        }

        private class RecordValueArguments
        {
            public ManualResetEvent MreToBlockUpdateThread;
            public ManualResetEvent MreToEnsureAllThreadsStart;
            public int ThreadsStartedCount;
            public Histogram<long> Histogram;
        }
    }
}
