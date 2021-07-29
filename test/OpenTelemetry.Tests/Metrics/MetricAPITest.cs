// <copyright file="MetricAPITest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTest
    {
        private static int numberOfThreads = 10;
        private static long deltaValueUpdatedByEachCall = 10;
        private static int numberOfMetricUpdateByEachThread = 1000000;

        [Fact]
        public void SimpleTest()
        {
            var metricItems = new List<MetricItem>();
            var metricExporter = new TestExporter<MetricItem>(ProcessExport);
            void ProcessExport(Batch<MetricItem> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var meter = new Meter("TestMeter");
            var counterLong = meter.CreateCounter<long>("mycounter");
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(new PushMetricProcessor(metricExporter, 100, isDelta: true))
                .Build();

            // setup args to threads.
            var mreToBlockUpdateThreads = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStarted = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments();
            argToThread.Counter = counterLong;
            argToThread.ThreadsStartedCount = 0;
            argToThread.MreToBlockUpdateThread = mreToBlockUpdateThreads;
            argToThread.MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStarted;

            Thread[] t = new Thread[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i] = new Thread(CounterUpdateThread);
                t[i].Start(argToThread);
            }

            // Block until all threads started.
            mreToEnsureAllThreadsStarted.WaitOne();

            // unblock all the threads.
            // (i.e let them start counter.Add)
            mreToBlockUpdateThreads.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            meterProvider.Dispose();

            // TODO: Once Dispose does flush, we may not need this
            // unknown sleep below.
            Thread.Sleep(1000);

            long sumReceived = 0;
            foreach (var metricItem in metricItems)
            {
                var metrics = metricItem.Metrics;
                foreach (var metric in metrics)
                {
                    sumReceived += (long)(metric as ISumMetric).Sum.Value;
                }
            }

            var expectedSum = deltaValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            Assert.Equal(expectedSum, sumReceived);
        }

        private static void CounterUpdateThread(object obj)
        {
            var arguments = obj as UpdateThreadArguments;
            if (arguments == null)
            {
                throw new Exception("Invalid args");
            }

            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var counter = arguments.Counter;

            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == numberOfThreads)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < numberOfMetricUpdateByEachThread; i++)
            {
                counter.Add(deltaValueUpdatedByEachCall, new KeyValuePair<string, object>("verb", "GET"));
            }
        }

        private class UpdateThreadArguments
        {
            public ManualResetEvent MreToBlockUpdateThread;
            public ManualResetEvent MreToEnsureAllThreadsStart;
            public int ThreadsStartedCount;
            public Counter<long> Counter;
        }
    }
}
