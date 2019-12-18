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
            var args = new Tuple<ManualResetEvent, CounterSumAggregator<long>>(mre, aggregator);
            Thread[] t = new Thread[10];
            for (int i = 0; i < 10; i++)
            {
                t[i] = new Thread(LongMetricUpdateThread);
                t[i].Start(args);
            }

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
            var args = new Tuple<ManualResetEvent, CounterSumAggregator<double>>(mre, aggregator);
            Thread[] t = new Thread[10];
            for (int i = 0; i < 10; i++)
            {
                t[i] = new Thread(DoubleMetricUpdateThread);
                t[i].Start(args);
            }

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
            var tuple = obj as Tuple<ManualResetEvent, CounterSumAggregator<long>>;
            var mre = tuple.Item1;
            var agg = tuple.Item2;

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10);
            }
        }

        private static void DoubleMetricUpdateThread(object obj)
        {
            var tuple = obj as Tuple<ManualResetEvent, CounterSumAggregator<double>>;
            var mre = tuple.Item1;
            var agg = tuple.Item2;

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < 1000000; i++)
            {
                agg.Update(10.5);
            }
        }
    }
}
