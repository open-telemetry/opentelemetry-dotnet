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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTest
    {
        private static int numberOfThreads = Environment.ProcessorCount;
        private static long deltaValueUpdatedByEachCall = 10;
        private static int numberOfMetricUpdateByEachThread = 100000;
        private readonly ITestOutputHelper output;

        public MetricApiTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CounterAggregationTest(bool exportDelta)
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

            var pullProcessor = new PullMetricProcessor(metricExporter, exportDelta);

            var meter = new Meter("TestMeter");
            var counterLong = meter.CreateCounter<long>("mycounter");
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(pullProcessor)
                .Build();

            counterLong.Add(10);
            counterLong.Add(10);
            pullProcessor.PullRequest();
            long sumReceived = GetSum(metricItems);
            Assert.Equal(20, sumReceived);

            metricItems.Clear();
            counterLong.Add(10);
            counterLong.Add(10);
            pullProcessor.PullRequest();
            sumReceived = GetSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(20, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            metricItems.Clear();
            pullProcessor.PullRequest();
            sumReceived = GetSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(0, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            metricItems.Clear();
            counterLong.Add(40);
            counterLong.Add(20);
            pullProcessor.PullRequest();
            sumReceived = GetSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(60, sumReceived);
            }
            else
            {
                Assert.Equal(100, sumReceived);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ObservableCounterAggregationTest(bool exportDelta)
        {
            var meterName = "TestMeter" + exportDelta;
            var metricItems = new List<MetricItem>();
            var metricExporter = new TestExporter<MetricItem>(ProcessExport);
            void ProcessExport(Batch<MetricItem> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var pullProcessor = new PullMetricProcessor(metricExporter, exportDelta);

            var meter = new Meter(meterName);
            int i = 1;
            var counterLong = meter.CreateObservableCounter<long>(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(i++ * 10),
                };
            });
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meterName)
                .AddMetricProcessor(pullProcessor)
                .Build();

            pullProcessor.PullRequest();
            long sumReceived = GetSum(metricItems);
            Assert.Equal(10, sumReceived);

            metricItems.Clear();
            pullProcessor.PullRequest();
            sumReceived = GetSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(10, sumReceived);
            }
            else
            {
                Assert.Equal(20, sumReceived);
            }

            metricItems.Clear();
            pullProcessor.PullRequest();
            sumReceived = GetSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(10, sumReceived);
            }
            else
            {
                Assert.Equal(30, sumReceived);
            }
        }

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

            var pullProcessor = new PullMetricProcessor(metricExporter, true);

            var meter = new Meter("TestMeter");
            var counterLong = meter.CreateCounter<long>("mycounter");
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(pullProcessor)
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

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // unblock all the threads.
            // (i.e let them start counter.Add)
            mreToBlockUpdateThreads.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            var timeTakenInMilliseconds = sw.ElapsedMilliseconds;
            this.output.WriteLine($"Took {timeTakenInMilliseconds} msecs. Total threads: {numberOfThreads}, each thread doing {numberOfMetricUpdateByEachThread} recordings.");

            meterProvider.Dispose();
            pullProcessor.PullRequest();

            long sumReceived = 0;
            foreach (var metricItem in metricItems)
            {
                var metrics = metricItem.Metrics;
                foreach (var metric in metrics)
                {
                    sumReceived += (metric as ISumMetricLong).LongSum;
                }
            }

            var expectedSum = deltaValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            Assert.Equal(expectedSum, sumReceived);
        }

        private static long GetSum(List<MetricItem> metricItems)
        {
            long sum = 0;
            foreach (var metricItem in metricItems)
            {
                var metrics = metricItem.Metrics;
                foreach (var metric in metrics)
                {
                    sum += (metric as ISumMetricLong).LongSum;
                }
            }

            return sum;
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
